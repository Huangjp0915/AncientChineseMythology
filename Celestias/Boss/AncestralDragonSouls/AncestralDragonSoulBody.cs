using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂身体段 - 跟随头部的身体节段
    /// 具有仙气飘渺的视觉效果
    /// </summary>
    public class AncestralDragonSoulBody : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Body;

        private int segmentIndex = 0;
        private float localPulseOffset;

        public override void ChangeSummonType() {
            // 根据召唤计数决定生成身体还是尾巴
            if (SummonCount >= SummonMax - 1) {
                SummonNPCType = ModContent.NPCType<AncestralDragonSoulTail>();
            }
            else {
                SummonNPCType = ModContent.NPCType<AncestralDragonSoulBody>();
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 70;
            NPC.height = 70;
            NPC.lifeMax = 3000000;
            NPC.damage = 160;
            NPC.defense = 70;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);

            segmentIndex = SummonCount;
            localPulseOffset = segmentIndex * 0.3f;
        }

        public override void AI() {
            base.AI();

            // 身体段特有的波动效果
            soulPulsePhase = globalTime * 2f + localPulseOffset;

            // 根据位置变化光照强度
            float pulseIntensity = 0.5f + MathF.Sin(soulPulsePhase) * 0.15f;

            // 身体段颜色随位置渐变（越往后越淡）
            float fadeRatio = (float)segmentIndex / 80f;
            float lightIntensity = (0.8f - fadeRatio * 0.3f) * pulseIntensity;

            Lighting.AddLight(NPC.Center, new Vector3(0.85f, 0.9f, 1f) * lightIntensity);
        }

        protected override void SpawnConnectionParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 midPoint = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;

            // 减少身体段的粒子数量
            int particleCount = (int)(NPC.velocity.Length() / 4);
            for (int i = 0; i < particleCount; i++) {
                if (Main.rand.NextBool(4)) {
                    int dustType = Main.rand.NextBool(4) ? DustID.Cloud : DustID.WhiteTorch;
                    int dust = Dust.NewDust(midPoint + Main.rand.NextVector2Circular(12, 12), 1, 1, dustType, 0, 0, 210, new Color(235, 240, 250), 1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.5f) * 0.25f;
                    Main.dust[dust].fadeIn = 1f;
                }
            }

            // 偶尔产生龙鳞光效
            if (Main.rand.NextBool(15)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(30, 30);
                int dust = Dust.NewDust(dustPos, 1, 1, DustID.Frost, 0, 0, 180, Color.White, 0.6f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 根据位置计算渐变透明度
            float fadeRatio = (float)segmentIndex / 80f;
            float segmentAlpha = 1f - fadeRatio * 0.25f;

            float soulPulse = 1f + MathF.Sin(soulPulsePhase) * 0.06f;

            // 外层光晕
            DrawSegmentGlow(spriteBatch, screenPos, tex, origin, soulPulse, segmentAlpha);

            // 主体
            Color mistColor = Color.Lerp(drawColor, new Color(235, 242, 255), 0.45f);
            mistColor = Color.Lerp(mistColor, Color.White, 0.25f + fadeRatio * 0.1f);
            mistColor *= segmentAlpha;

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * soulPulse, effects, 0f);

            // 内层光晕
            Color innerGlow = new Color(255, 255, 255) * 0.2f * soulPulse * segmentAlpha;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.85f, effects, 0f);

            return false;
        }

        private void DrawSegmentGlow(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse, float alpha) {
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 双层光晕
            for (int i = 0; i < 2; i++) {
                float layerScale = 1.08f + i * 0.08f + MathF.Sin(soulPulsePhase * 1.5f + i) * 0.03f;
                float layerAlpha = (0.18f - i * 0.05f) * mistAlpha * alpha;

                Color layerColor = i == 0 ? new Color(255, 255, 255) : new Color(220, 235, 255);
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 offset = new Vector2(MathF.Sin(globalTime * 1.5f + segmentIndex * 0.2f + i), MathF.Cos(globalTime + segmentIndex * 0.2f + i)) * 2f;

                spriteBatch.Draw(tex, NPC.Center + offset - screenPos, null, layerColor,
                    NPC.rotation, origin, NPC.scale * layerScale * pulse, effects, 0f);
            }
        }

        public override void OnKill() {
            // 身体段死亡粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

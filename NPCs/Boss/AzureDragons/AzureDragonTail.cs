using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙尾部 - 带有尾焰电弧特效
    /// </summary>
    public class AzureDragonTail : AzureDragon
    {
        public override WormType NPCWormType => WormType.Tail;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 50;
            NPC.height = 50;
        }

        public override void AI() {
            base.AI();

            // 尾部额外的电弧粒子
            if (!VaultUtils.isServer && NPC.velocity.LengthSquared() > 9f && Main.rand.NextBool(2)) {
                Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f;
                dustPos += Main.rand.NextVector2Circular(20, 20);
                int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.2f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 贴图朝右为正方向，向左飞行时垂直翻转
            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // rotation已是原始速度角度
            Main.EntitySpriteDraw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, 1f, effects);

            // 尾部光效
            DrawSegmentGlow(spriteBatch, screenPos, drawColor);

            // 尾焰效果
            DrawTailFlame(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 尾部青色闪电尾焰
        /// </summary>
        private void DrawTailFlame(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.SlashBurst == null) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float speed = NPC.velocity.Length();
            float flameIntensity = MathHelper.Clamp(speed / 15f, 0.2f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(segmentPulsePhase * 2f);

            Color flameColor = DragonCyan * (0.4f * flameIntensity * pulse);
            flameColor.A = 0;

            // SlashBurst作为尾焰光束（从底部发散的效果正好做尾焰）
            Vector2 flameOrigin = new(ACMAsset.SlashBurst.Width / 2f, ACMAsset.SlashBurst.Height * 0.8f);
            float flameRot = NPC.rotation + MathHelper.Pi; // 朝尾部反方向喷射
            float flameScale = 0.15f + 0.08f * flameIntensity;

            spriteBatch.Draw(ACMAsset.SlashBurst, NPC.Center - screenPos, null, flameColor,
                flameRot, flameOrigin, new Vector2(flameScale * 0.6f, flameScale * (1f + flameIntensity * 0.5f)),
                SpriteEffects.None, 0f);

            // 闪电分叉尾焰
            if (ACMAsset.LightningBranch != null && flameIntensity > 0.4f) {
                float branchPulse = MathF.Sin(segmentPulsePhase * 3f) * 0.5f + 0.5f;
                Color branchColor = DragonLightning * (0.25f * branchPulse * flameIntensity);
                branchColor.A = 0;

                Vector2 branchOrigin = new(ACMAsset.LightningBranch.Width / 2f, ACMAsset.LightningBranch.Height * 0.9f);
                float branchScale = 0.08f + 0.04f * flameIntensity;

                spriteBatch.Draw(ACMAsset.LightningBranch, NPC.Center - screenPos, null, branchColor,
                    flameRot + Main.rand.NextFloat(-0.15f, 0.15f), branchOrigin,
                    new Vector2(branchScale * 0.5f, branchScale),
                    SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}

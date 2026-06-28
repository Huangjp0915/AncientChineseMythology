using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙身体段 - 带有青蓝光效脉动的体节。
    /// V2: 不再是被动跟随 — 头部引导大招时, 体节蓄"电荷"并沿龙身波次同步放出雷弹 (导电龙身机制)。
    /// </summary>
    public class AzureDragonBody : AzureDragon
    {
        public override WormType NPCWormType => WormType.Body;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
        }

        /// <summary>读取头部是否正在引导大招 (导电龙身的触发条件)。</summary>
        private bool HeadChanneling {
            get {
                if (NPC.realLife < 0 || NPC.realLife >= Main.maxNPCs)
                    return false;
                NPC head = Main.npc[NPC.realLife];
                return head.active && head.ModNPC is AzureDragonHead h && h.BodyChannelActive;
            }
        }

        public override void AI() {
            base.AI();

            bool channel = HeadChanneling;
            segmentGlowIntensity = MathHelper.Lerp(segmentGlowIntensity, channel ? 2.2f : 1f, 0.06f);

            // —— 导电电荷拖尾 (引导期更强): 沿龙身的青电弧 ——
            if (!VaultUtils.isServer && channel && Main.rand.NextBool(2)) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.5f, NPC.height * 0.5f);
                int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 60, default, 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
            }

            // —— 节段同步雷弹: 沿龙身的电荷波依次从间隔体节放出 (服务器权威) ——
            if (channel && SummonCount % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                const int period = 36;
                int phase = SummonCount * 3;
                if ((Main.GameUpdateCount + (ulong)phase) % period == 0) {
                    Vector2 forward = NPC.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
                    int side = (SummonCount % 16 == 0) ? 1 : -1;
                    Vector2 vel = perp * side * 8.5f;
                    int dmg = Math.Max(1, NPC.damage / 8);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<AzureBolt>(), dmg, 1f);

                    SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.5f, Volume = 0.4f }, NPC.Center);
                }
            }
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AzureDragonBody>();
            if (SummonCount >= SummonMax - 5)
                SummonNPCType = ModContent.NPCType<AzureDragonTail>();
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 贴图朝右为正方向，向左飞行时垂直翻转
            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // rotation已是原始速度角度（IsUseSpriteDirection=false时PostAI不会叠加Pi）
            Main.EntitySpriteDraw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, 1f, effects);

            // 体节光效脉动
            DrawSegmentGlow(spriteBatch, screenPos, drawColor);

            // 体节间的能量流动效果
            DrawEnergyFlow(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 沿体节绘制能量流动光效
        /// </summary>
        private void DrawEnergyFlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 能量流动波纹 - 使用SummonCount创建延迟波纹效果
            float wavePhase = segmentPulsePhase - SummonCount * 0.15f;
            float wavePulse = MathF.Max(0, MathF.Sin(wavePhase));

            if (wavePulse > 0.1f) {
                Color flowColor = DragonCyan * (0.35f * wavePulse);
                flowColor.A = 0;

                Vector2 flowOrigin = new(ACMAsset.LightShot.Width / 2f, ACMAsset.LightShot.Height / 2f);
                float flowScale = 0.8f + 0.4f * wavePulse;

                spriteBatch.Draw(ACMAsset.LightShot, NPC.Center - screenPos, null, flowColor,
                    NPC.rotation, flowOrigin, flowScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}

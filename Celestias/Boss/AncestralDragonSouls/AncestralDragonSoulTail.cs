using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂尾部 - 龙尾末端，具有独特的视觉效果和攻击能力
    /// </summary>
    public class AncestralDragonSoulTail : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Tail;

        private float tailSwayPhase;
        private int attackCooldown = 180;
        private float trailIntensity = 0f;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.lifeMax = 3000000;
            NPC.damage = 180;
            NPC.defense = 60;
        }

        public override void AI() {
            base.AI();

            tailSwayPhase += 0.1f;
            soulPulsePhase = globalTime * 2.5f;

            // 尾巴摆动效果
            float swayAmount = MathF.Sin(tailSwayPhase) * 0.15f;
            if (FatherNPC != null && FatherNPC.active) {
                Vector2 perpendicular = NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                NPC.position += perpendicular * swayAmount * 3f;
            }

            // 尾巴攻击
            attackCooldown--;
            if (attackCooldown <= 0) {
                attackCooldown = Main.expertMode ? 120 : 150;
                PerformTailAttack();
            }

            // 拖尾强度根据速度变化
            trailIntensity = MathHelper.Lerp(trailIntensity, MathHelper.Clamp(NPC.velocity.Length() / 20f, 0.3f, 1f), 0.1f);

            // 尾部发光更强
            float pulseIntensity = 0.7f + MathF.Sin(soulPulsePhase) * 0.25f;
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.95f, 1f) * pulseIntensity);
        }

        private void PerformTailAttack() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 获取头部引用来找到目标
            NPC headNPC = null;
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                headNPC = Main.npc[NPC.realLife];
            }

            if (headNPC == null) return;

            Player target = Main.player[headNPC.target];
            if (!target.active || target.dead) return;

            // 发射龙尾扫击波
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = -2; i <= 2; i++) {
                float angle = MathHelper.ToRadians(i * 20);
                Vector2 vel = toPlayer.RotatedBy(angle) * 10f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    vel,
                    ModContent.ProjectileType<TailSweepWave>(),
                    NPC.damage / 3,
                    2f
                );
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);

            // 扫击粒子
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Cloud, vel.X, vel.Y, 180, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        protected override void SpawnConnectionParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 midPoint = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;

            // 尾部连接粒子
            int particleCount = (int)(NPC.velocity.Length() / 3);
            for (int i = 0; i < particleCount; i++) {
                if (Main.rand.NextBool(3)) {
                    int dustType = Main.rand.NextBool(3) ? DustID.Cloud : DustID.WhiteTorch;
                    int dust = Dust.NewDust(midPoint + Main.rand.NextVector2Circular(10, 10), 1, 1, dustType, 0, 0, 200, new Color(230, 240, 255), 1.1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.4f) * 0.2f;
                }
            }

            // 尾部特有的拖尾光效
            if (Main.rand.NextBool(4) && trailIntensity > 0.5f) {
                Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f;
                dustPos += Main.rand.NextVector2Circular(15, 15);
                int dust = Dust.NewDust(dustPos, 1, 1, DustID.Clentaminator_Cyan, 0, 0, 150, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.1f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float soulPulse = 1f + MathF.Sin(soulPulsePhase) * 0.08f;

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 绘制拖尾
            DrawTailTrail(spriteBatch, screenPos, tex, origin, effects);

            // 外层光晕
            DrawTailGlow(spriteBatch, screenPos, tex, origin, soulPulse, effects);

            // 主体
            Color mistColor = Color.Lerp(drawColor, new Color(240, 245, 255), 0.5f);
            mistColor = Color.Lerp(mistColor, Color.White, 0.3f);

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation + MathHelper.PiOver2, origin, NPC.scale * soulPulse, effects, 0f);

            // 尾尖光效
            DrawTailTip(spriteBatch, screenPos);

            return false;
        }

        private void DrawTailTrail(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, SpriteEffects effects) {
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                float trailAlpha = progress * 0.25f * trailIntensity;

                // 渐变拖尾
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(200, 220, 255), 1f - progress);
                trailColor *= trailAlpha;
                trailColor.A = 0;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(tex, pos, null, trailColor,
                    NPC.oldRot[i] + MathHelper.PiOver2, origin, scale, effects, 0f);
            }
        }

        private void DrawTailGlow(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse, SpriteEffects effects) {
            // 三层光晕
            for (int i = 0; i < 3; i++) {
                float layerScale = 1.1f + i * 0.1f + MathF.Sin(soulPulsePhase * 2f + i) * 0.04f;
                float layerAlpha = (0.22f - i * 0.05f) * mistAlpha;

                Color layerColor = i switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(220, 240, 255),
                    _ => new Color(200, 220, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 offset = new Vector2(MathF.Sin(globalTime * 2f + i), MathF.Cos(globalTime * 1.5f + i)) * 3f;

                spriteBatch.Draw(tex, NPC.Center + offset - screenPos, null, layerColor,
                    NPC.rotation + MathHelper.PiOver2, origin, NPC.scale * layerScale * pulse, effects, 0f);
            }
        }

        private void DrawTailTip(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            // 尾尖发光
            Vector2 tipOffset = -NPC.rotation.ToRotationVector2() * 25f;
            Vector2 tipPos = NPC.Center + tipOffset - screenPos;

            float tipPulse = 0.6f + MathF.Sin(globalTime * 3f) * 0.3f;
            Color tipColor = new Color(220, 240, 255) * tipPulse * 0.5f;
            tipColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, tipPos, null, tipColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.5f * tipPulse, SpriteEffects.None, 0f);

            // 额外的星光效果
            if (ACMAsset.Sparkle != null && trailIntensity > 0.6f) {
                Color sparkleColor = new Color(255, 255, 255) * trailIntensity * 0.3f;
                sparkleColor.A = 0;
                spriteBatch.Draw(ACMAsset.Sparkle, tipPos, null, sparkleColor, globalTime * 2f,
                    ACMAsset.Sparkle.Size() / 2f, 0.4f * tipPulse, SpriteEffects.None, 0f);
            }
        }

        public override void OnKill() {
            // 尾部死亡粒子爆发
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 7);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Frost
                };
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f }, NPC.Center);
        }
    }
}

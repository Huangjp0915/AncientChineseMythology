using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float waterPulse = 1f + MathF.Sin(globalTime * 3f) * 0.08f;

            // 水光晕
            DrawWaterAura(spriteBatch, screenPos, tex, origin, waterPulse);

            // 拖尾
            DrawWaterTrail(spriteBatch, screenPos, tex, origin);

            // 主体
            Color waterTint = Color.Lerp(drawColor, AoGuangHelper.DragonBlue, 0.4f);
            waterTint = Color.Lerp(waterTint, Color.White, 0.2f);

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 外层发光
            Color outerGlow = AoGuangHelper.WaterGlow * 0.4f * waterPulse;
            outerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, outerGlow,
                NPC.rotation, origin, NPC.scale * 1.15f * waterPulse, effects, 0f);

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, waterTint * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * waterPulse, effects, 0f);

            // 内层高光
            Color innerGlow = AoGuangHelper.PureWhite * 0.3f * waterPulse;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.8f, effects, 0f);

            // 龙眼光效
            DrawDragonEyes(spriteBatch, screenPos);

            return false;
        }

        private void DrawWaterAura(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse) {
            if (waterAuraAlpha <= 0f) return;

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 多层水波纹
            for (int i = 3; i >= 0; i--) {
                float layerAlpha = waterAuraAlpha * (0.15f - i * 0.03f);
                float layerScale = waveScale * (1.3f + i * 0.15f);
                float layerRot = waveRotation * (1f + i * 0.2f);

                Color layerColor = Color.Lerp(AoGuangHelper.DragonBlue, AoGuangHelper.OceanTeal, i / 3f);
                layerColor *= layerAlpha * pulse;
                layerColor.A = 0;

                spriteBatch.Draw(tex, NPC.Center - screenPos, null, layerColor,
                    NPC.rotation + layerRot * (i % 2 == 0 ? 1 : -1), origin, NPC.scale * layerScale, effects, 0f);
            }
        }

        private void DrawWaterTrail(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin) {
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.35f;
                trailColor.A = 0;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float trailScale = NPC.scale * (0.9f - i * 0.04f);
                float trailRot = NPC.oldRot.Length > i ? NPC.oldRot[i] : NPC.rotation;

                spriteBatch.Draw(tex, pos, null, trailColor, trailRot, origin, trailScale, effects, 0f);
            }
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            // 龙眼位置偏移
            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 35f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.7f + MathF.Sin(globalTime * 5f) * 0.3f;

            // 根据阶段改变眼睛颜色
            Color eyeColor;
            if (IsPhase3) {
                eyeColor = Color.Lerp(AoGuangHelper.DragonBlue, Color.Red, 0.3f);
            }
            else if (IsPhase2) {
                eyeColor = AoGuangHelper.WaterGlow;
            }
            else {
                eyeColor = AoGuangHelper.DragonBlue;
            }

            eyeColor *= eyePulse * 0.8f;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.5f * eyePulse * glowIntensity, SpriteEffects.None, 0f);
        }

        #endregion

        #region 死亡效果

        public override void OnKill() {
            // 标记击败
            Systems.DownedBossSystem.downedAoGuang = true;

            // 死亡粒子爆发
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 120; i++) {
                    float angle = MathHelper.TwoPi * i / 120;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6, 18);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.BubbleBlock
                    };
                    int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                }

                // 巨型水花
                for (int wave = 0; wave < 3; wave++) {
                    for (int i = 0; i < 40; i++) {
                        float angle = MathHelper.TwoPi * i / 40 + wave * 0.3f;
                        float speed = 10f + wave * 5f;
                        Vector2 vel = angle.ToRotationVector2() * speed;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Water, vel.X, vel.Y, 80, default, 4f - wave * 0.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCDeath62 with { Volume = 1.8f }, NPC.Center);
        }

        #endregion
    }
}

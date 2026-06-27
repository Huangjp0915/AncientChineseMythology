using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 绘制方法（分离文件）
    /// </summary>
    internal partial class Vaisravana
    {
        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // V2 演出层（底层，位于本体之下）：终极宝塔金链 / 镜射安全轴 / 赐福金闪
            DrawPagodaApexChannel();
            DrawMirrorAxisTell();
            DrawBlessingStealFlash();

            // 绘制神圣光环（底层）
            DrawDivineAura(spriteBatch, screenPos);

            // 绘制拖尾
            DrawTrail(spriteBatch, screenPos);

            // 绘制宝塔
            DrawTowers(spriteBatch, screenPos, drawColor);

            // 绘制光晕（在本体之前）
            DrawHalo(spriteBatch, screenPos);

            // 绘制本体
            DrawMainBody(spriteBatch, screenPos, drawColor);

            // 绘制外层光效
            DrawOuterGlow(spriteBatch, screenPos);

            // 守护反震护盾
            if (guardVisual > 0.01f)
                DrawGuardShield(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 终极宝塔（Pagoda Apex）金链汇能演出：蓄力期四塔金光经 <see cref="ACMShaders.DrawBeam"/> 金带汇入本体宝塔,
        /// 配 <see cref="ACMShaders.DrawRadialBloomAt"/> 金色库藏泛光; 蓄满发射后金柱由 <see cref="TreasureTowerRay"/> 接管。
        /// 暖金=危险蓄力, 与 70 tick 可读蓄力完全同步。仅客户端、受配置降级（DrawBeam/DrawRadialBloomAt 内建守护）。
        /// </summary>
        private void DrawPagodaApexChannel() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase3_UltimateTower)
                return;

            bool charging = (int)SubState == 0;
            float charge = charging ? MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f) : 1f;

            // 四塔金链汇向本体宝塔（蓄力越满越亮越宽）
            float channelIntensity = 0.30f + charge * 0.70f;
            Color core = VaisravanaHelper.TowerGold; core.A = 255;
            Color edge = TelegraphColors.Gold; edge.A = 120;
            float halfWidth = 8f + charge * 16f;
            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i);
                ACMShaders.DrawBeam(towerPos, NPC.Center, halfWidth, core, edge, channelIntensity,
                    flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.0f);
            }

            // 本体宝塔顶库藏金爆（蓄满最强；发射期保持）
            float bloom = charging ? 0.15f + charge * charge * 0.6f : 0.85f;
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.14f + charge * 0.1f, bloom, TelegraphColors.Gold,
                rayCount: 14f, falloff: 2.4f);
        }

        /// <summary>夜叉镜射 B 幕：蓄力期沿镜轴画一条金色安全轴 telegraph（金=安全可穿越, 非致命）。</summary>
        private void DrawMirrorAxisTell() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase3_YakshaMirror || (int)SubState != 0)
                return;

            float grow = MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f);
            Vector2 dir = mirrorAxis.ToRotationVector2();
            Vector2 start = NPC.Center - dir * 1400f;
            Vector2 end = NPC.Center + dir * 1400f;
            Color core = TelegraphColors.Holy; core.A = 200;
            Color edge = VaisravanaHelper.CelestialAzure; edge.A = 60;
            ACMShaders.DrawBeam(start, end, 5f + grow * 4f, core, edge, 0.35f + grow * 0.35f,
                flowSpeed: 1.0f, flowScale: 3.0f, coreSharp: 3.2f);
        }

        /// <summary>赐福窃取金闪：被窃取宝塔处迸射 <see cref="ACMShaders.DrawRadialBloomAt"/> 金光（"我抢到了"反馈）。</summary>
        private void DrawBlessingStealFlash() {
            if (VaultUtils.isServer || blessingFlash <= 0 || lastBlessedTower < 0 || lastBlessedTower >= TowerCount)
                return;

            float t = blessingFlash / 26f;
            ACMShaders.DrawRadialBloomAt(GetTowerPosition(lastBlessedTower), 0.06f + (1f - t) * 0.05f,
                t * 0.55f, TelegraphColors.Gold, rayCount: 10f, falloff: 2.8f);
        }

        /// <summary>守护姿态护盾 — 借鉴玄武 绝对防御 的盾环反馈。</summary>
        private void DrawGuardShield(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D tex = ACMAsset.BlankStar;
            if (tex == null) return;

            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = tex.Size() / 2f;
            float radius = 150f;
            int segs = 28;
            Color shield = VaisravanaHelper.TowerGold * MathHelper.Clamp(guardVisual, 0f, 1.6f) * 0.5f;
            shield.A = 0;

            for (int i = 0; i < segs; i++) {
                float angle = haloRotation * 2f + MathHelper.TwoPi * i / segs;
                Vector2 pos = drawPos + angle.ToRotationVector2() * radius;
                spriteBatch.Draw(tex, pos, null, shield, angle, origin, 0.3f, SpriteEffects.None, 0f);
            }

            if (ACMAsset.SoftGlow != null) {
                Color core = VaisravanaHelper.PureWhite * MathHelper.Clamp(guardVisual, 0f, 1.6f) * 0.25f;
                core.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawPos, null, core, 0f, ACMAsset.SoftGlow.Size() / 2f, radius / 40f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>绘制某座宝塔的充能格（金=满，暗=空）与赐福区提示。</summary>
        private void DrawTowerCharges(SpriteBatch spriteBatch, Vector2 screenPos, int index) {
            if (towerCharges == null) return;
            Texture2D dot = ACMAsset.LightShot;
            if (dot == null) return;

            Vector2 towerWorld = GetTowerPosition(index);
            Vector2 towerPos = towerWorld - screenPos;
            Vector2 origin = dot.Size() / 2f;

            // 充能格围绕宝塔小环排布
            for (int c = 0; c < MaxTowerCharge; c++) {
                float a = -MathHelper.PiOver2 + (c - (MaxTowerCharge - 1) / 2f) * 0.5f;
                Vector2 pipPos = towerPos + a.ToRotationVector2() * 34f;
                bool filled = c < towerCharges[index];
                Color pip = filled ? VaisravanaHelper.TowerGold : VaisravanaHelper.CelestialAzure * 0.3f;
                pip.A = 0;
                spriteBatch.Draw(dot, pipPos, null, pip * (filled ? 0.9f : 0.4f), 0f, origin, filled ? 0.22f : 0.16f, SpriteEffects.None, 0f);
            }

            // 赐福区提示：有充能时画一圈淡环（窃取闪光时高亮）
            if (towerCharges[index] > 0) {
                bool flash = blessingFlash > 0 && lastBlessedTower == index;
                float zoneAlpha = flash ? 0.6f : 0.14f;
                int segs = 18;
                for (int i = 0; i < segs; i++) {
                    float angle = MathHelper.TwoPi * i / segs + globalTime;
                    Vector2 pos = towerPos + angle.ToRotationVector2() * BlessingRadius;
                    Color ring = (flash ? VaisravanaHelper.PureWhite : VaisravanaHelper.TowerGold) * zoneAlpha;
                    ring.A = 0;
                    spriteBatch.Draw(dot, pos, null, ring, 0f, origin, flash ? 0.22f : 0.14f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawDivineAura(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            Texture2D auraTexture = ACMAsset.LightShot;
            Vector2 drawPos = NPC.Center - screenPos;

            // 仙气白色光环
            Color auraColor = VaisravanaHelper.PureWhite * divineAuraAlpha;
            auraColor.A = 0;

            float auraScale = 9f * haloScale;

            spriteBatch.Draw(
                auraTexture,
                drawPos,
                null,
                auraColor,
                MathHelper.PiOver2,
                auraTexture.Size() / 2f,
                auraScale,
                SpriteEffects.None,
                0f
            );

            // 第二层淡蓝光环
            Color azureAura = VaisravanaHelper.CelestialAzure * divineAuraAlpha * 0.5f;
            azureAura.A = 0;

            spriteBatch.Draw(
                auraTexture,
                drawPos,
                null,
                azureAura,
                MathHelper.PiOver2,
                auraTexture.Size() / 2f,
                auraScale * 1.3f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = VaisravanaHelper.SpiritSilver * progress * 0.25f * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTowers(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (towerAngles == null) return;

            // 使用BlankStar绘制宝塔效果（可替换为专用纹理）
            Texture2D towerTexture = ACMAsset.BlankStar;
            if (towerTexture == null) return;

            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i) - screenPos;

                // 外层金光晕
                Color outerGlow = VaisravanaHelper.TowerGold * 0.5f;
                outerGlow.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    outerGlow,
                    globalTime + i * 0.6f,
                    towerTexture.Size() / 2f,
                    0.7f,
                    SpriteEffects.None,
                    0f
                );

                // 核心白光
                Color coreColor = VaisravanaHelper.PureWhite;
                coreColor.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    coreColor,
                    -globalTime * 0.6f + i * 0.4f,
                    towerTexture.Size() / 2f,
                    0.45f,
                    SpriteEffects.None,
                    0f
                );

                // 内核高光
                Color innerCore = VaisravanaHelper.DivineWhite;
                innerCore.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    innerCore * 0.8f,
                    0f,
                    towerTexture.Size() / 2f,
                    0.3f,
                    SpriteEffects.None,
                    0f
                );

                // 宝塔连接线效果
                if (ACMAsset.GlaciateWave != null) {
                    Vector2 toCenter = NPC.Center - GetTowerPosition(i);
                    float distance = toCenter.Length();
                    float rotation = toCenter.ToRotation();

                    Color lineColor = VaisravanaHelper.SpiritSilver * 0.3f;
                    lineColor.A = 0;

                    Vector2 lineOrigin = new Vector2(0, ACMAsset.GlaciateWave.Height / 2f);
                    Vector2 lineScale = new Vector2(distance / ACMAsset.GlaciateWave.Width, 0.05f);

                    spriteBatch.Draw(
                        ACMAsset.GlaciateWave,
                        GetTowerPosition(i) - screenPos,
                        null,
                        lineColor,
                        rotation,
                        lineOrigin,
                        lineScale,
                        SpriteEffects.None,
                        0f
                    );
                }

                // 宝塔充能格 + 赐福区提示
                DrawTowerCharges(spriteBatch, screenPos, i);
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null) return;

            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;

            // 多层光环
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.6f + i * 0.35f) * haloScale;
                Color layerColor = VaisravanaHelper.PureWhite * (0.35f - i * 0.08f);
                layerColor.A = 0;

                spriteBatch.Draw(
                    haloTexture,
                    drawPos,
                    null,
                    layerColor,
                    layerRotation,
                    haloTexture.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 反向旋转的淡蓝光环
            Color azureHalo = VaisravanaHelper.CelestialAzure * 0.25f;
            azureHalo.A = 0;
            spriteBatch.Draw(
                haloTexture,
                drawPos,
                null,
                azureHalo,
                -haloRotation * 0.7f,
                haloTexture.Size() / 2f,
                2f * haloScale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 内层发光
            Color glowColor = VaisravanaHelper.PureWhite * 0.35f * NPC.Opacity;
            glowColor.A = 0;

            for (int i = 0; i < 4; i++) {
                float angle = globalTime * 1.8f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                spriteBatch.Draw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 1.08f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 本体
            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                bodyColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale,
                SpriteEffects.None,
                0f
            );

            // 高光叠加
            Color highlightColor = VaisravanaHelper.DivineWhite * 0.2f * NPC.Opacity;
            highlightColor.A = 0;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                highlightColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale * 0.95f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.Sparkle == null) return;

            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;

            Color sparkleColor = VaisravanaHelper.PureWhite * 0.28f * glowIntensity;
            sparkleColor.A = 0;

            // 旋转的星芒
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                sparkleColor,
                globalTime * 0.4f,
                sparkleTexture.Size() / 2f,
                2.2f * haloScale,
                SpriteEffects.None,
                0f
            );

            // 反向旋转的星芒
            Color secondarySparkle = VaisravanaHelper.CelestialAzure * 0.18f * glowIntensity;
            secondarySparkle.A = 0;
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                secondarySparkle,
                -globalTime * 0.25f,
                sparkleTexture.Size() / 2f,
                2.8f * haloScale,
                SpriteEffects.None,
                0f
            );

            // 三阶段额外光效
            if (IsPhase3 && ACMAsset.LightShot != null) {
                float pulseAlpha = 0.15f + MathF.Sin(globalTime * 4f) * 0.08f;
                Color pulseColor = VaisravanaHelper.DivineWhite * pulseAlpha;
                pulseColor.A = 0;

                spriteBatch.Draw(
                    ACMAsset.LightShot,
                    drawPos,
                    null,
                    pulseColor,
                    0f,
                    ACMAsset.LightShot.Size() / 2f,
                    5f * haloScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        #endregion
    }
}

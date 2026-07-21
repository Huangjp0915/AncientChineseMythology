using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region 着色器缓存（Xuanwu 写法：自身代码内静态缓存，不注册 ACMShaders）

        private static Asset<Effect> stormWarpShaderRef;

        private static Effect GetStormWarpShader() {
            stormWarpShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/AoshunStormWarp", AssetRequestMode.ImmediateLoad);
            return stormWarpShaderRef?.Value;
        }

        #endregion

        #region 头部绘制

        /// <summary>
        /// 头部绘制 — 纹理Aoshun.png: 52×140, 2帧, 每帧52×70。
        /// V3：穿刺预警线（红=致命契约）+ 冲刺残影（速度门控）+ 蓄电光晕 + 死亡渐隐白热。
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int frameHeight = texture.Height / HeadFrameCount;
            int yPos = frameHeight * NPC.frame.Y;
            Rectangle sourceRectangle = new Rectangle(0, yPos, texture.Width, frameHeight);

            Vector2 origin = NPC.spriteDirection == -1
                ? new Vector2(texture.Width * 0.5f + 10, frameHeight * 0.5f + 16)
                : new Vector2(texture.Width - 10, frameHeight + 16);

            Vector2 drawPos = NPC.Center - screenPos;

            // === 穿刺预警线（红线 = 致命契约, 与音效同窗口） ===
            DrawDashTelegraph();

            // === 冲刺残影: 速度门控 —— 只有真正快的时刻才出现 ===
            if (dashVisualHeat > 0.25f && NPC.velocity.Length() > 24f) {
                Vector2 back = -NPC.velocity.SafeNormalize(Vector2.UnitX);
                for (int g = 1; g <= 3; g++) {
                    float ghostA = dashVisualHeat * (0.32f - g * 0.08f);
                    Color ghost = AoshunHelper.NorthSeaCyan * ghostA;
                    ghost.A = 0;
                    spriteBatch.Draw(texture, drawPos + back * g * 22f, sourceRectangle, ghost,
                        NPC.rotation, origin, NPC.scale * (1f - g * 0.04f), effects, 0);
                }
            }

            // === 风暴蓄电光晕 ===
            float chargeRatio = StormCharge / MaxStormCharge;
            if (chargeRatio > 0.2f) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float pulse = 1f + MathF.Sin(globalTime * 4f) * 0.15f;
                    float auraScale = (0.8f + chargeRatio * 0.8f) * pulse;
                    float auraAlpha = chargeRatio * 0.35f;

                    Color outerGlow = AoshunHelper.ThunderPurple * auraAlpha * 0.5f;
                    outerGlow.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, outerGlow, 0f, glowOrigin, auraScale * 1.5f, SpriteEffects.None, 0f);

                    Color innerGlow = AoshunHelper.LightningBlue * auraAlpha * 0.7f;
                    innerGlow.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, innerGlow, 0f, glowOrigin, auraScale, SpriteEffects.None, 0f);

                    if (IsFullyCharged) {
                        float flash = MathF.Sin(globalTime * 8f) * 0.5f + 0.5f;
                        Color whiteFlash = AoshunHelper.ElectricWhite * flash * 0.4f;
                        whiteFlash.A = 0;
                        spriteBatch.Draw(glowTex, drawPos, null, whiteFlash, 0f, glowOrigin, auraScale * 0.6f, SpriteEffects.None, 0f);
                    }
                }

                // 高蓄电电弧装饰
                Texture2D arcSheet = ACMAsset.ElectricArcSheet;
                if (arcSheet != null && chargeRatio > 0.5f) {
                    int arcIndex = ((int)(globalTime * 8f)) % 4;
                    int arcHeight = arcSheet.Height / 4;
                    Rectangle arcSourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                    Vector2 arcOrigin = new Vector2(arcSourceRect.Width / 2f, arcSourceRect.Height / 2f);

                    float arcAlpha = (chargeRatio - 0.5f) * 2f * 0.35f;
                    Color arcColor = AoshunHelper.LightningBlue * arcAlpha;
                    arcColor.A = 0;

                    spriteBatch.Draw(arcSheet, drawPos + new Vector2(-20, -5), arcSourceRect, arcColor,
                        NPC.rotation + MathHelper.PiOver4, arcOrigin, 0.1f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(arcSheet, drawPos + new Vector2(20, -5), arcSourceRect, arcColor * 0.8f,
                        NPC.rotation - MathHelper.PiOver4, arcOrigin, 0.1f, SpriteEffects.FlipHorizontally, 0f);
                }
            }

            // === 换阶段演出的环体强光 ===
            if (CurrentState is AoshunState.Transition2 or AoshunState.Transition3) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    float transitionPulse = MathF.Sin(globalTime * 6f) * 0.5f + 0.5f;
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    Color transColor = AoshunHelper.ElectricWhite * transitionPulse * 0.6f;
                    transColor.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, transColor, globalTime * 2f, glowOrigin, 2.5f, SpriteEffects.None, 0f);
                }
            }

            // === 主体（蓄电偏青 / 死亡渐隐白热） ===
            Color finalDrawColor = drawColor;
            if (chargeRatio > 0.5f) {
                float tint = (chargeRatio - 0.5f) * 2f;
                finalDrawColor = Color.Lerp(drawColor, Color.Lerp(drawColor, AoshunHelper.LightningBlue, 0.3f), tint);
            }
            finalDrawColor = AoshunHelper.ApplyDeathFade(finalDrawColor, DeathProgress);

            spriteBatch.Draw(texture, drawPos,
                sourceRectangle, finalDrawColor, NPC.rotation, origin, NPC.scale, effects, 0);

            if (DeathProgress > 0f && ACMAsset.SoftGlow != null) {
                Color white = AoshunHelper.ElectricWhite * DeathProgress * 0.5f;
                white.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawPos, null, white, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.8f, SpriteEffects.None, 0f);
            }

            return false;
        }

        /// <summary>
        /// 穿刺预警线: 风暴穿刺(锁线后 20f 窗口) / 眼弦穿刺(锁弦后 20f 窗口)。
        /// 红 = 致命契约; 窗口判定完全由同步状态推导, 各端一致。
        /// </summary>
        private void DrawDashTelegraph() {
            if (CurrentState != AoshunState.Attacking)
                return;

            float alpha = 0f;
            Vector2 lineStart = NPC.Center;
            switch (CurrentAttack) {
                case AoshunAttackType.TempestPierce:
                    // 蓄势后半: 10f 锁线 → 30f 发射
                    if (SubState % 2 == 0 && StateTimer >= 10 && StateTimer < 30) {
                        alpha = (StateTimer - 10) / 20f;
                        lineStart = NPC.Center;
                    }
                    break;
                case AoshunAttackType.EyePierce:
                    // 就位后半: 25f 锁弦 → 45f 入弦（预警线从入弦点画穿眼）
                    if (SubState % 2 == 0 && StateTimer >= 25 && StateTimer < 45) {
                        alpha = (StateTimer - 25) / 20f;
                        lineStart = aimPoint;
                    }
                    break;
            }

            if (alpha <= 0.03f || aimVector.LengthSquared() < 0.01f)
                return;

            Vector2 dir = aimVector.SafeNormalize(Vector2.UnitX);
            float width = 3f + alpha * 5f;
            ACMShaders.DrawBeam(lineStart, lineStart + dir * 1500f, width,
                TelegraphColors.Lethal, TelegraphColors.Lethal * 0.5f, alpha * 0.7f,
                flowSpeed: 4f, flowScale: 1.6f, coreSharp: 3f);
        }

        public override void FindFrame(int frameHeight) {
            // 张嘴: 贴近玩家 / 冲刺爆发中 / 破土上冲 / 死亡末段嘶吼
            bool piercing = CurrentState == AoshunState.Attacking &&
                (CurrentAttack is AoshunAttackType.TempestPierce or AoshunAttackType.EyePierce) &&
                SubState % 2 == 1 && StateTimer <= 12;
            bool breaching = CurrentState == AoshunState.Attacking &&
                CurrentAttack == AoshunAttackType.AbyssBreach && SubState == 3;
            bool deathRoar = CurrentState == AoshunState.Dying && StateTimer >= 150;

            NPC.frame.Y = (close || piercing || breaching || deathRoar) ? 1 : 0;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation;
        }

        #endregion

        #region PostDraw：满电电网 + 全屏风暴扭曲（名额契约）

        /// <summary>
        /// ● 满电"雷暴临界": 蛇身段间连成通电电网（DrawBeam 硬化 API, 纯视觉）。
        /// ● 全屏风暴扭曲(AoshunStormWarp): 风场卷动 + 斜向雨幕 + 眼内平静区,
        ///   走 RequestFullscreenSlot 名额契约（同屏 ≤1 全屏后处理）, 强度 &lt;0.01 直接跳过。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            DrawCinematicBolts();
            DrawChargedNet();
            DrawStormWarpOverlay(spriteBatch);
        }

        /// <summary>
        /// 演出巨雷（顶点光束）: T2「巨雷贯体」单道粗雷; 死亡「万雷加身」三道并落。
        /// 只在各自 25f 窗口内渐灭 — 全战最重的两次视觉重拍。
        /// </summary>
        private void DrawCinematicBolts() {
            float fade = 0f;
            int boltCount = 0;

            if (CurrentState == AoshunState.Transition2 && StateTimer >= 120 && StateTimer < 145) {
                fade = 1f - (StateTimer - 120) / 25f;
                boltCount = 1;
            }
            else if (CurrentState == AoshunState.Dying && StateTimer >= 150 && StateTimer < 175) {
                fade = 1f - (StateTimer - 150) / 25f;
                boltCount = 3;
            }

            if (boltCount == 0 || fade <= 0.02f)
                return;

            for (int i = 0; i < boltCount; i++) {
                float xOff = (i - (boltCount - 1) * 0.5f) * 120f;
                Vector2 hit = NPC.Center + new Vector2(xOff * 0.2f, 0);
                Vector2 sky = hit + new Vector2(xOff, -1500f);
                float jitter = MathF.Sin(globalTime * 40f + i * 2.1f) * 6f * fade;
                ACMShaders.DrawBeam(sky + new Vector2(jitter, 0), hit, 26f * fade + 5f,
                    AoshunHelper.ElectricWhite, AoshunHelper.LightningBlue, fade,
                    flowSpeed: 6f, flowScale: 1.4f, coreSharp: 2.6f, coreGlow: 1.3f);
            }
        }

        /// <summary>满电电网: ≥82% 电量渐显（把"雷暴临界"留作高潮而非常驻）。</summary>
        private void DrawChargedNet() {
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            float chargeRatio = MathHelper.Clamp(StormCharge / MaxStormCharge, 0f, 1f);
            float netT = MathHelper.Clamp((chargeRatio - 0.82f) / 0.18f, 0f, 1f);
            netT = netT * netT * (3f - 2f * netT);
            // 死亡演出静默拍后: 电网常亮明灭（只剩它在呼吸）
            if (CurrentState == AoshunState.Dying && StateTimer >= 90)
                netT = Math.Max(netT, 0.75f);
            if (netT <= 0.01f)
                return;

            float pulse = 0.7f + MathF.Sin(globalTime * 6f) * 0.3f;
            float intensity = netT * pulse;
            float halfWidth = 7f * pulse;
            bool sparse = MythologyConfig.Trail == TrailQualityLevel.Med;

            Color core = TelegraphColors.Lightning;
            Color edge = AoshunHelper.ThunderPurple;

            int drawn = 0;
            foreach (NPC seg in Main.ActiveNPCs) {
                if (seg.realLife != NPC.whoAmI || seg.whoAmI == NPC.whoAmI)
                    continue;
                if (seg.type != ModContent.NPCType<AoshunBody>() &&
                    seg.type != ModContent.NPCType<AoshunArms>() &&
                    seg.type != ModContent.NPCType<AoshunTail>())
                    continue;

                int prevIndex = (int)seg.ai[1];
                if (prevIndex < 0 || prevIndex >= Main.maxNPCs)
                    continue;
                NPC prev = Main.npc[prevIndex];
                if (!prev.active)
                    continue;

                drawn++;
                if (sparse && (drawn & 1) == 0)
                    continue; // 中端质量: 隔段连接, 减半批次

                ACMShaders.DrawBeam(prev.Center, seg.Center, halfWidth, core, edge, intensity,
                    flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.4f);
            }
        }

        /// <summary>
        /// 全屏风暴扭曲: T2 后风雨常驻（stormWeatherFx 在 AI 侧推进）, 风向取自风场池
        /// —— 雨幕/扭曲/风线共用同一气流语言; 风暴之眼激活时眼内被着色器抠成平静区。
        /// </summary>
        private void DrawStormWarpOverlay(SpriteBatch sb) {
            float intensity = stormWeatherFx;
            if (intensity <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = GetStormWarpShader();
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWindDir"]?.SetValue(AoshunWindField.CurrentWindDir);
            fx.Parameters["uWind"]?.SetValue(0.55f + 0.45f * MathF.Sin(globalTime * 0.37f));
            fx.Parameters["uRain"]?.SetValue(MathHelper.Clamp(intensity * 1.15f, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(AoshunSky.CurrentFlashWhite);

            if (EyeActive && EyeRadius > 16f) {
                ACMShaders.WorldDecalParams(EyeCenter, EyeRadius, out Vector2 eyeUv, out float radiusFrac, out _);
                fx.Parameters["uEyeCenter"]?.SetValue(eyeUv);
                fx.Parameters["uEyeRadius"]?.SetValue(radiusFrac);
            }
            else {
                fx.Parameters["uEyeCenter"]?.SetValue(new Vector2(0.5f, 0.5f));
                fx.Parameters["uEyeRadius"]?.SetValue(0f);
            }

            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: true);
        }

        #endregion
    }
}

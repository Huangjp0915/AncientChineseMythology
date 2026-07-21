using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 绘制层（分离文件）。
    /// V3：金身法相着色器本体 / 宝塔专属贴图化 / 三面六臂法相 / 宝伞格挡 /
    /// 天王步落点坛城 + 各招式 DrawBeam 预警带 / 死亡龟裂。
    /// </summary>
    internal partial class Vaisravana
    {
        #region 绘制主入口

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // —— 地面/世界层预警（位于一切实体绘制之下）——
            DrawIntroMandala();
            DrawStepTelegraph();
            DrawVajraChargeTell();
            DrawVolleyChargeLines();
            DrawSweepScanLine();
            DrawQuadrantTell();
            DrawSealSafeLaneTell();
            DrawMirrorAxisTell();
            DrawPagodaApexChannel();
            DrawBlessingStealFlash();

            // —— 本体层 ——
            DrawDivineAura(spriteBatch, screenPos);
            DrawTrail(spriteBatch, screenPos);
            DrawDharmaAura(spriteBatch, screenPos);
            DrawTowers(spriteBatch, screenPos, drawColor);
            DrawHalo(spriteBatch, screenPos);
            DrawMainBody(spriteBatch, screenPos, drawColor);
            DrawOuterGlow(spriteBatch, screenPos);

            // —— 宝伞格挡（覆于本体之上）——
            if (umbrellaOpen > 0.02f || guardVisual > 0.05f)
                DrawUmbrella(spriteBatch, screenPos);

            return false;
        }

        #endregion

        #region 演出层 · 坛城 / 预警带

        /// <summary>入场落场拍：脚下坛城弹性展开（70f 落地 → 130f 静观期间保持余晖）。</summary>
        private void DrawIntroMandala() {
            if (VaultUtils.isServer || Phase != BossPhase.Intro || PhaseTimer < 70f)
                return;

            float t = (float)PhaseTimer - 70f;
            float expand = ACMUtils.BackOut(MathHelper.Clamp(t / 26f, 0f, 1f));
            float fade = t < 60f ? 1f : MathHelper.Clamp(1f - (t - 60f) / 55f, 0.35f, 1f);
            Vector2 feet = NPC.Center + new Vector2(0, 60f);
            VaisravanaHelper.DrawMandalaInBatch(feet, 420f * expand, 0.85f * fade,
                MathHelper.Clamp(t / 26f, 0f, 1f), haloRotation * 2f);
        }

        /// <summary>
        /// 天王步落点预警：架步期在 dashTarget 画小坛城（reveal 随架步进度）+ 一条淡金指向线。
        /// 覆盖 P1 天王三步与 P2 天王踏阵。
        /// </summary>
        private void DrawStepTelegraph() {
            if (VaultUtils.isServer)
                return;
            bool stepping = (Phase == BossPhase.Phase1_KingSteps || Phase == BossPhase.Phase2_StampFormation)
                            && (int)SubState == 0;
            if (!stepping || dashTarget == Vector2.Zero)
                return;

            int windup = Phase == BossPhase.Phase1_KingSteps
                ? (dashCount == 0 ? 50 : 30)
                : (dashCount >= 3 ? 34 : Math.Max(20, 26 - dashCount * 2));
            float t = MathHelper.Clamp(PhaseTimer / windup, 0f, 1f);

            bool bigStamp = Phase == BossPhase.Phase2_StampFormation && dashCount >= 3;
            float radius = bigStamp ? 260f : 150f;
            VaisravanaHelper.DrawMandalaInBatch(dashTarget, radius, 0.35f + t * 0.5f, t,
                globalTime * 1.5f);

            // 指向线：淡金，仅提示方向不构成威胁色
            Color lineCore = VaisravanaHelper.TowerGold; lineCore.A = 90;
            Color lineEdge = TelegraphColors.Gold; lineEdge.A = 40;
            ACMShaders.DrawBeam(NPC.Center, dashTarget, 4f + t * 3f, lineCore, lineEdge,
                0.16f + t * 0.22f, flowSpeed: 2.6f, flowScale: 3f, coreSharp: 3f);
        }

        /// <summary>
        /// 金刚破军蓄力预警：矛线金带渐亮；瞄准锁死后中央亮起致命红芯（全模组唯一红=真伤害）。
        /// 静默段(SubState 1)保持满亮度定格。
        /// </summary>
        private void DrawVajraChargeTell() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase1_VajraPierce || (int)SubState > 1)
                return;

            float charge = (int)SubState == 1 ? 1f : MathHelper.Clamp(PhaseTimer / 66f, 0f, 1f);
            bool locked = (int)SubState == 1 || PhaseTimer >= 44;

            Vector2 dir = laserAngle.ToRotationVector2();
            Vector2 start = NPC.Center + dir * 60f;
            Vector2 end = NPC.Center + dir * 2300f;

            Color core = TelegraphColors.Gold; core.A = 160;
            Color edge = VaisravanaHelper.TowerGold; edge.A = 60;
            ACMShaders.DrawBeam(start, end, 9f + charge * 9f, core, edge, 0.28f + charge * 0.4f,
                flowSpeed: 1.8f, flowScale: 2.4f, coreSharp: 2.4f);

            if (locked) {
                Color lethal = TelegraphColors.Lethal; lethal.A = 200;
                Color lethalEdge = TelegraphColors.Lethal; lethalEdge.A = 0;
                ACMShaders.DrawBeam(start, end, 3.5f, lethal, lethalEdge, 0.65f,
                    flowSpeed: 3.5f, flowScale: 1.5f, coreSharp: 3.4f);
            }
        }

        /// <summary>宝塔齐射蓄力：带充能宝塔→玩家的金线渐亮（赐福变体为翠玉安全色）。</summary>
        private void DrawVolleyChargeLines() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase1_TowerVolley || (int)SubState != 0 || volleyCharge <= 0.02f)
                return;

            Player target = Main.player[NPC.target];
            if (!target.active)
                return;

            Color core = volleyBlessed ? TelegraphColors.Safe : TelegraphColors.Gold;
            core.A = 140;
            Color edge = volleyBlessed ? TelegraphColors.Safe : VaisravanaHelper.TowerGold;
            edge.A = 40;
            for (int i = 0; i < TowerCount; i++) {
                if (towerCharges[i] <= 0) continue;
                Vector2 from = GetTowerPosition(i);
                ACMShaders.DrawBeam(from, target.Center, 2.5f + volleyCharge * 5f, core, edge,
                    0.15f + volleyCharge * 0.4f, flowSpeed: 2.2f, flowScale: 2.8f, coreSharp: 2.8f);
            }
        }

        /// <summary>威光扫射预告：扇形边界双线（淡）+ 从起始角扫到终止角的明亮扫描线。</summary>
        private void DrawSweepScanLine() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase1_SweepingLight || (int)SubState != 0 || sweepTelegraph <= 0.02f)
                return;

            float startAngle = (laserSweepDirection > 0 ? -MathHelper.PiOver4 : MathHelper.PiOver4) + MathHelper.PiOver2;
            float endAngle = (laserSweepDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4) + MathHelper.PiOver2;

            Color faint = VaisravanaHelper.TowerGold; faint.A = 60;
            Color faintEdge = VaisravanaHelper.TowerGold; faintEdge.A = 0;
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + startAngle.ToRotationVector2() * 1100f, 3f,
                faint, faintEdge, 0.22f * sweepTelegraph, coreSharp: 3f);
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + endAngle.ToRotationVector2() * 1100f, 3f,
                faint, faintEdge, 0.22f * sweepTelegraph, coreSharp: 3f);

            // 扫描线：预演一遍扫射路径
            float scanAngle = MathHelper.Lerp(startAngle, endAngle, sweepTelegraph);
            Color scan = TelegraphColors.Gold; scan.A = 170;
            Color scanEdge = TelegraphColors.Gold; scanEdge.A = 30;
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + scanAngle.ToRotationVector2() * 1100f,
                5f, scan, scanEdge, 0.5f * sweepTelegraph, flowSpeed: 3f, coreSharp: 2.6f);
        }

        /// <summary>四象射线预告：非安全方向金带渐亮（安全道不画=负空间提示）。</summary>
        private void DrawQuadrantTell() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase2_QuadrantRay || (int)SubState != 0)
                return;

            float t = MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f);
            Color core = TelegraphColors.Gold; core.A = 150;
            Color edge = VaisravanaHelper.TowerGold; edge.A = 40;
            for (int c = 0; c < 4; c++) {
                if (!YakshaAlive(Opposite(c))) continue; // 安全道
                Vector2 dir = CardinalAngle[c].ToRotationVector2();
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1700f, 4f + t * 7f, core, edge,
                    0.2f + t * 0.42f, flowSpeed: 2f, flowScale: 2.5f, coreSharp: 2.5f);
            }
        }

        /// <summary>金环收束 A 幕预告：安全道扇区两条翠玉边线 + 中央淡坛城。</summary>
        private void DrawSealSafeLaneTell() {
            if (VaultUtils.isServer || Phase != BossPhase.Phase3_SealRings || (int)SubState != 0)
                return;

            float t = MathHelper.Clamp(PhaseTimer / 55f, 0f, 1f);
            Color safe = TelegraphColors.Safe; safe.A = 130;
            Color safeEdge = TelegraphColors.Safe; safeEdge.A = 0;
            for (int s = -1; s <= 1; s += 2) {
                float a = laserAngle + s * 0.66f; // 与 TreasurySealRing.SafeHalfWidth 对齐
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + a.ToRotationVector2() * 1000f, 4f,
                    safe, safeEdge, 0.2f + t * 0.35f, flowSpeed: 1.6f, coreSharp: 3f);
            }

            VaisravanaHelper.DrawMandalaInBatch(NPC.Center, 200f, 0.4f * t, t, -globalTime);
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

        /// <summary>
        /// 终极宝塔（Pagoda Apex）金链汇能演出：蓄力期四塔金光经 DrawBeam 金带汇入本体，
        /// 配 DrawRadialBloomAt 金色库藏泛光; 蓄满发射后金柱由 TreasureTowerRay 接管。
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

        /// <summary>赐福窃取金闪：被窃取宝塔处迸射径向金光（"我抢到了"反馈）。</summary>
        private void DrawBlessingStealFlash() {
            if (VaultUtils.isServer || blessingFlash <= 0 || lastBlessedTower < 0 || lastBlessedTower >= TowerCount)
                return;

            float t = blessingFlash / 26f;
            ACMShaders.DrawRadialBloomAt(GetTowerPosition(lastBlessedTower), 0.06f + (1f - t) * 0.05f,
                t * 0.55f, TelegraphColors.Gold, rayCount: 10f, falloff: 2.8f);
        }

        #endregion

        #region 本体层 · 法相 / 宝伞 / 光环

        /// <summary>
        /// 三面六臂法相：本体后方两层旋转加性金身虚影 + 六道臂光从身后扇形展开。
        /// dharmaAura 由换阶段演出驱动至 1，P2/P3 常驻低强度。
        /// </summary>
        private void DrawDharmaAura(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (dharmaAura <= 0.03f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = texture.Size() / 2f;
            float bodyScale = NPC.scale * IntroScaleFactor();

            // 六臂光轮：身后扇形光线（LightShot 拉伸），随法相强度展开
            if (ACMAsset.LightShot != null) {
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 3; i++) {
                        float baseAngle = -MathHelper.PiOver2 + side * MathHelper.ToRadians(38f + i * 30f);
                        float sway = MathF.Sin(globalTime * 1.3f + i * 1.1f + side) * 0.06f;
                        float angle = baseAngle + sway;
                        float reach = (95f + i * 30f) * dharmaAura * bodyScale;
                        Vector2 armPos = drawPos + angle.ToRotationVector2() * reach * 0.55f;
                        Color armColor = VaisravanaHelper.TowerGold * (0.34f - i * 0.07f) * dharmaAura;
                        armColor.A = 0;
                        spriteBatch.Draw(ACMAsset.LightShot, armPos, null, armColor, angle,
                            ACMAsset.LightShot.Size() / 2f, new Vector2(reach / 90f, 0.5f * dharmaAura), SpriteEffects.None, 0f);
                    }
                }
            }

            // 双层旋转虚影（三面读感：左右各偏转一份的金身残像）
            for (int side = -1; side <= 1; side += 2) {
                float wobble = MathF.Sin(globalTime * 1.7f + side * 2f) * 0.02f;
                float rot = NPC.rotation + side * (0.16f + 0.05f * dharmaAura) + wobble;
                Color ghost = VaisravanaHelper.ImmortalGold * 0.30f * dharmaAura * NPC.Opacity;
                ghost.A = 0;
                spriteBatch.Draw(texture, drawPos, null, ghost, rot, origin,
                    bodyScale * (1.14f + 0.1f * dharmaAura), SpriteEffects.None, 0f);
            }
            Color outerGhost = TelegraphColors.Gold * 0.16f * dharmaAura * NPC.Opacity;
            outerGhost.A = 0;
            spriteBatch.Draw(texture, drawPos, null, outerGhost, NPC.rotation, origin,
                bodyScale * (1.30f + 0.12f * dharmaAura), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 宝伞格挡：面向玩家的弧形伞盖（金瓣canopy + 伞骨 + 中柱），随 umbrellaOpen 展开。
        /// 展开度即无敌窗口的状态广播——伞开=收手走位，伞收=输出窗口。
        /// </summary>
        private void DrawUmbrella(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D petal = ACMAsset.LightShot;
            Texture2D rib = ACMAsset.GlaciateWave;
            if (petal == null || rib == null)
                return;

            Player target = Main.player[NPC.target];
            float faceAngle = target.active
                ? (target.Center - NPC.Center).ToRotation()
                : -MathHelper.PiOver2;

            float open = ACMUtils.BackOut(MathHelper.Clamp(umbrellaOpen, 0f, 1f));
            float radius = 155f * open;
            float span = MathHelper.Lerp(MathHelper.ToRadians(50f), MathHelper.ToRadians(205f), open);
            Vector2 drawPos = NPC.Center - screenPos;
            float bright = 0.55f + MathHelper.Clamp(guardVisual, 0f, 1.6f) * 0.35f;

            // 伞骨（每 2 瓣一根，从中心放射）
            const int segs = 11;
            for (int i = 0; i < segs; i += 2) {
                float a = faceAngle - span * 0.5f + span * i / (segs - 1);
                Color ribColor = VaisravanaHelper.SpiritSilver * 0.3f * open;
                ribColor.A = 0;
                Vector2 ribOrigin = new(0, rib.Height / 2f);
                spriteBatch.Draw(rib, drawPos, null, ribColor, a, ribOrigin,
                    new Vector2(radius / rib.Width, 0.03f), SpriteEffects.None, 0f);
            }

            // 伞盖弧面（金瓣拼接，双层）
            for (int i = 0; i < segs; i++) {
                float a = faceAngle - span * 0.5f + span * i / (segs - 1);
                Vector2 pos = drawPos + a.ToRotationVector2() * radius;
                float petalPulse = 1f + MathF.Sin(globalTime * 3f + i * 0.7f) * 0.08f;

                Color canopyGold = VaisravanaHelper.TowerGold * (0.5f * bright) * open;
                canopyGold.A = 0;
                spriteBatch.Draw(petal, pos, null, canopyGold, a + MathHelper.PiOver2,
                    petal.Size() / 2f, new Vector2(0.62f, 0.30f) * petalPulse, SpriteEffects.None, 0f);

                Color canopyRim = VaisravanaHelper.PureWhite * (0.30f * bright) * open;
                canopyRim.A = 0;
                spriteBatch.Draw(petal, pos, null, canopyRim, a + MathHelper.PiOver2,
                    petal.Size() / 2f, new Vector2(0.40f, 0.16f) * petalPulse, SpriteEffects.None, 0f);
            }

            // 伞顶明珠（面向轴线尽头）
            if (ACMAsset.SoftGlow != null) {
                Vector2 tip = drawPos + faceAngle.ToRotationVector2() * (radius + 18f);
                Color pearl = VaisravanaHelper.DivineWhite * (0.5f * bright) * open;
                pearl.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, tip, null, pearl, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.5f * open, SpriteEffects.None, 0f);
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
            float rise = towerRise[index];
            if (rise < 0.35f) return;

            // 充能格围绕宝塔小环排布
            for (int c = 0; c < MaxTowerCharge; c++) {
                float a = -MathHelper.PiOver2 + (c - (MaxTowerCharge - 1) / 2f) * 0.5f;
                Vector2 pipPos = towerPos + a.ToRotationVector2() * 40f;
                bool filled = c < towerCharges[index];
                Color pip = filled ? VaisravanaHelper.TowerGold : VaisravanaHelper.CelestialAzure * 0.3f;
                pip.A = 0;
                spriteBatch.Draw(dot, pipPos, null, pip * (filled ? 0.9f : 0.4f) * rise, 0f, origin, filled ? 0.22f : 0.16f, SpriteEffects.None, 0f);
            }

            // 赐福区提示：有充能时画一圈淡环（窃取闪光时高亮）
            if (towerCharges[index] > 0) {
                bool flash = blessingFlash > 0 && lastBlessedTower == index;
                float zoneAlpha = (flash ? 0.6f : 0.14f) * rise;
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
            float silence = particleSilence ? 0.25f : 1f; // 死亡终爆前的静默：光效同步压暗

            // 仙气白色光环
            Color auraColor = VaisravanaHelper.PureWhite * divineAuraAlpha * NPC.Opacity * silence;
            auraColor.A = 0;

            float auraScale = 9f * haloScale * IntroScaleFactor();

            spriteBatch.Draw(auraTexture, drawPos, null, auraColor, MathHelper.PiOver2,
                auraTexture.Size() / 2f, auraScale, SpriteEffects.None, 0f);

            // 第二层淡蓝光环
            Color azureAura = VaisravanaHelper.CelestialAzure * divineAuraAlpha * 0.5f * NPC.Opacity;
            azureAura.A = 0;

            spriteBatch.Draw(auraTexture, drawPos, null, azureAura, MathHelper.PiOver2,
                auraTexture.Size() / 2f, auraScale * 1.3f, SpriteEffects.None, 0f);
        }

        /// <summary>速度门控残影：只在天王步等高速瞬间可见（速度卖点自动增幅）。</summary>
        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            float speed = NPC.velocity.Length();
            float speedGate = MathHelper.Clamp((speed - 9f) / 22f, 0f, 1f);
            if (speedGate <= 0.03f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = VaisravanaHelper.TowerGold * progress * 0.4f * speedGate * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.95f;

                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation,
                    texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>宝塔：专属贴图 VaisravanaTower + 底座金晕 + 升起/后座动画 + 充能格。</summary>
        private void DrawTowers(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (towerAngles == null || towerRise == null) return;

            Texture2D towerTex = VaisravanaHelper.TowerTexture;
            if (towerTex == null) return;
            Vector2 towerOrigin = towerTex.Size() / 2f;

            for (int i = 0; i < TowerCount; i++) {
                float rise = towerRise[i];
                if (rise <= 0.02f) continue;

                Vector2 towerWorld = GetTowerPosition(i);
                // 后座偏移：发射瞬间塔身向外弹开
                Vector2 recoilDir = (towerWorld - NPC.Center).SafeNormalize(Vector2.UnitY);
                Vector2 towerPos = towerWorld - screenPos + recoilDir * towerRecoil[i] * 14f;

                float bob = MathF.Sin(globalTime * 2.2f + i * 1.7f) * 3f;
                towerPos.Y += bob + (1f - rise) * 46f;
                float towerScale = (0.9f + MathF.Sin(globalTime * 1.6f + i) * 0.05f) * MathHelper.Lerp(0.4f, 1f, rise);
                float alpha = rise * NPC.Opacity;

                // 底座金晕
                if (ACMAsset.SoftGlow != null) {
                    Color baseGlow = VaisravanaHelper.TowerGold * (0.4f + towerCharges[i] * 0.1f) * alpha;
                    baseGlow.A = 0;
                    spriteBatch.Draw(ACMAsset.SoftGlow, towerPos, null, baseGlow, 0f,
                        ACMAsset.SoftGlow.Size() / 2f, 1.15f * towerScale, SpriteEffects.None, 0f);
                }

                // 金光衬影 + 塔体 + 高光
                Color aura = TelegraphColors.Gold * 0.55f * alpha;
                aura.A = 0;
                spriteBatch.Draw(towerTex, towerPos, null, aura, 0f, towerOrigin, towerScale * 1.16f, SpriteEffects.None, 0f);
                spriteBatch.Draw(towerTex, towerPos, null, Color.White * alpha, 0f, towerOrigin, towerScale, SpriteEffects.None, 0f);
                Color highlight = VaisravanaHelper.DivineWhite * 0.35f * alpha;
                highlight.A = 0;
                spriteBatch.Draw(towerTex, towerPos, null, highlight, 0f, towerOrigin, towerScale * 0.82f, SpriteEffects.None, 0f);

                // 与本体的灵脉连接线
                if (ACMAsset.GlaciateWave != null) {
                    Vector2 toCenter = NPC.Center - towerWorld;
                    float distance = toCenter.Length();
                    float rotation = toCenter.ToRotation();

                    Color lineColor = VaisravanaHelper.SpiritSilver * 0.25f * alpha;
                    lineColor.A = 0;

                    Vector2 lineOrigin = new(0, ACMAsset.GlaciateWave.Height / 2f);
                    Vector2 lineScale = new(distance / ACMAsset.GlaciateWave.Width, 0.05f);

                    spriteBatch.Draw(ACMAsset.GlaciateWave, towerWorld - screenPos, null, lineColor,
                        rotation, lineOrigin, lineScale, SpriteEffects.None, 0f);
                }

                // 宝塔充能格 + 赐福区提示
                DrawTowerCharges(spriteBatch, screenPos, i);
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null) return;

            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;
            float introScale = IntroScaleFactor();

            // 死亡演出：光轮高频闪烁失稳
            float deathFlicker = 1f;
            if (Phase == BossPhase.Death && PhaseTimer < 200f)
                deathFlicker = 0.65f + 0.35f * MathF.Sin((float)PhaseTimer * 0.9f + MathF.Sin((float)PhaseTimer * 0.23f) * 3f);

            // 多层光环
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.6f + i * 0.35f) * haloScale * introScale;
                Color layerColor = VaisravanaHelper.PureWhite * (0.35f - i * 0.08f) * deathFlicker * NPC.Opacity;
                layerColor.A = 0;

                spriteBatch.Draw(haloTexture, drawPos, null, layerColor, layerRotation,
                    haloTexture.Size() / 2f, layerScale, SpriteEffects.None, 0f);
            }

            // 反向旋转的淡蓝光环
            Color azureHalo = VaisravanaHelper.CelestialAzure * 0.25f * deathFlicker * NPC.Opacity;
            azureHalo.A = 0;
            spriteBatch.Draw(haloTexture, drawPos, null, azureHalo, -haloRotation * 0.7f,
                haloTexture.Size() / 2f, 2f * haloScale * introScale, SpriteEffects.None, 0f);
        }

        /// <summary>入场假 Z 缩放：0~70f 从背景 0.12 冲镜放大至 1（cubed 已并入 introProgress）。</summary>
        private float IntroScaleFactor() {
            if (Phase != BossPhase.Intro)
                return 1f;
            return 0.12f + 0.88f * introProgress;
        }

        /// <summary>
        /// 本体：金身法相着色器单 pass（rim 金边 + 体内金纹流动 + 白闪 + 死亡龟裂），
        /// 着色器不可用时退化为普通绘制。
        /// </summary>
        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            float bodyScale = NPC.scale * IntroScaleFactor();
            Vector2 origin = texture.Size() / 2f;

            Effect fx = VaisravanaHelper.GoldBodyShader;
            Texture2D noise = ACMShaders.NoiseTexture;

            if (fx != null && noise != null) {
                float intensity = 0.5f + divineAuraAlpha * 0.4f + chargeConverge * 0.3f;

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1.2f));
                fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / texture.Width, 1f / texture.Height));
                fx.Parameters["uRimColor"]?.SetValue(TelegraphColors.Gold.ToVector4());
                fx.Parameters["uFlowColor"]?.SetValue(VaisravanaHelper.TowerGold.ToVector4());
                fx.Parameters["uFlashWhite"]?.SetValue(MathHelper.Clamp(bodyFlash, 0f, 1f));
                fx.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(bodyCrack, 0f, 1f));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.Draw(texture, drawPos, null, drawColor * NPC.Opacity, NPC.rotation,
                    origin, bodyScale, SpriteEffects.None, 0f);

                spriteBatch.End();
                ACMShaders.RestoreDefaultBatch(spriteBatch);
            }
            else {
                // 退化路径：普通本体 + 高光
                Color bodyColor = drawColor * NPC.Opacity;
                spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation, origin, bodyScale, SpriteEffects.None, 0f);

                Color highlightColor = VaisravanaHelper.DivineWhite * 0.2f * NPC.Opacity;
                highlightColor.A = 0;
                spriteBatch.Draw(texture, drawPos, null, highlightColor, NPC.rotation, origin, bodyScale * 0.95f, SpriteEffects.None, 0f);
            }
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.Sparkle == null) return;

            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;
            float introScale = IntroScaleFactor();

            Color sparkleColor = VaisravanaHelper.PureWhite * 0.28f * glowIntensity * NPC.Opacity;
            sparkleColor.A = 0;

            // 旋转的星芒
            spriteBatch.Draw(sparkleTexture, drawPos, null, sparkleColor, globalTime * 0.4f,
                sparkleTexture.Size() / 2f, 2.2f * haloScale * introScale, SpriteEffects.None, 0f);

            // 反向旋转的星芒
            Color secondarySparkle = VaisravanaHelper.CelestialAzure * 0.18f * glowIntensity * NPC.Opacity;
            secondarySparkle.A = 0;
            spriteBatch.Draw(sparkleTexture, drawPos, null, secondarySparkle, -globalTime * 0.25f,
                sparkleTexture.Size() / 2f, 2.8f * haloScale * introScale, SpriteEffects.None, 0f);

            // 三阶段额外光效
            if (IsPhase3 && ACMAsset.LightShot != null) {
                float pulseAlpha = 0.15f + MathF.Sin(globalTime * 4f) * 0.08f;
                Color pulseColor = VaisravanaHelper.DivineWhite * pulseAlpha * NPC.Opacity;
                pulseColor.A = 0;

                spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, pulseColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 5f * haloScale, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}

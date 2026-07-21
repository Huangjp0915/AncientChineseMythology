using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 路径预警特效 - 显示冲刺/弹幕将要划过的路线 (V3: BeamGrad 光束化, 金→末段转红)
    /// ai[0] = 目标X, ai[1] = 目标Y, ai[2] = 存活帧数(0=默认60)
    /// </summary>
    public class CelestialPathWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private Vector2 StartPos;
        private Vector2 EndPos;
        private int lifeTotal = 60;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                StartPos = Projectile.Center;
                EndPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                if (Projectile.ai[2] > 0) {
                    lifeTotal = (int)Projectile.ai[2];
                    Projectile.timeLeft = lifeTotal;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (StartPos == Vector2.Zero || EndPos == Vector2.Zero)
                return false;

            float life = 1f - Projectile.timeLeft / (float)lifeTotal; // 0→1
            // 淡入 12f, 末端保持; 最后 30f 由金转红 (致命预警)
            float fadeIn = MathHelper.Clamp(life * lifeTotal / 12f, 0f, 1f);
            float redPhase = MathHelper.Clamp((30f - Projectile.timeLeft) / 30f, 0f, 1f);
            float flicker = redPhase > 0f ? 0.8f + MathF.Sin(Projectile.timeLeft * 0.7f) * 0.2f : 1f;

            Color core = Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, redPhase);
            Color edge = Color.Lerp(TelegraphColors.Holy, TelegraphColors.Lethal, redPhase * 0.7f);
            core.A = 200;
            edge.A = 60;

            float width = MathHelper.Lerp(5f, 11f, redPhase);
            ACMShaders.DrawBeam(StartPos, EndPos, width, core, edge,
                fadeIn * flicker * 0.85f, 2.4f, 2.0f, 2.6f);

            // 终点落点标记
            Texture2D tex = ACMAsset.LightShot;
            if (tex != null) {
                Color mark = core;
                mark.A = 0;
                float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 9f) * 0.25f;
                Main.EntitySpriteDraw(tex, EndPos - Main.screenPosition, null, mark * (fadeIn * 0.8f),
                    0f, tex.Size() / 2f, (0.5f + redPhase * 0.3f) * pulse, SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 闪电预警特效 - 竖直预警线
    /// </summary>
    public class CelestialLightningWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Projectile.timeLeft > 60)
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, (90 - Projectile.timeLeft) / 30f);
            else
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, (60 - Projectile.timeLeft) / 60f);

            if (Projectile.timeLeft == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0, 20f),
                    ModContent.ProjectileType<CelestialLightning>(), 70, 5f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot;
            Color color = Color.Red * (1f - Projectile.alpha / 255f) * 0.6f;
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, new Vector2(0.15f, 3f), SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 金色闪电
    /// </summary>
    public class CelestialLightning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 100;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);

                if (!Main.dedServ) {
                    for (int i = 0; i < 20; i++) {
                        Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 8);
                        int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                            DustID.Electric, vel.X, vel.Y, 100, Color.Gold, 1.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            Projectile.alpha = (int)MathHelper.Lerp(100, 255, 1f - Projectile.timeLeft / 60f);

            // 拖尾粒子
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave;
            float scale = 0.3f + (1f - Projectile.timeLeft / 60f) * 0.2f;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.velocity.ToRotation(), tex.Size() / 2f, scale, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 金色剑气
    /// </summary>
    public class GoldenSwordAura : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 50;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, 0.2f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 金色能量弹 - 辐射弹幕
    /// </summary>
    public class GoldenEnergy : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 400;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 50;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ) {
                Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0f);

                if (Main.rand.NextBool(4)) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, 0, 0, 100, default, 0.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.3f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot;
            Color color = Color.Gold;
            color.A = 0;

            float scale = 0.7f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.1f;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(2, 2);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 下落的金剑
    /// </summary>
    public class FallingSword : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.velocity.Y < 24f)
                Projectile.velocity.Y += 0.4f;

            if (!Main.dedServ && Main.rand.NextBool()) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            // 纵向落线指引 (慢起步 + 可预读的下落走廊)
            Texture2D line = ACMAsset.LightShot;
            if (line != null) {
                Color guide = Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, 0.5f) * 0.22f;
                guide.A = 0;
                Main.EntitySpriteDraw(line, Projectile.Center + new Vector2(0f, 450f) - Main.screenPosition, null, guide,
                    MathHelper.PiOver2, line.Size() / 2f, new Vector2(14f, 0.09f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, new Vector2(tex.Width / 2f, tex.Height * 0.25f), 0.15f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 15; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 龙威法阵预警 - 显示法阵即将出现的位置
    /// </summary>
    public class DragonCircleWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float warningScale = 0f;
        private float runeRotation = 0f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90; // 1.5秒预警时间
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            runeRotation += 0.05f;

            // 预警展开
            if (Projectile.timeLeft > 60) {
                warningScale = MathHelper.Lerp(warningScale, 1f, 0.08f);
            }
            // 预警闪烁加速
            else if (Projectile.timeLeft < 30) {
                warningScale *= 0.95f;
            }

            // 预警粒子
            if (Main.rand.NextBool(3) && warningScale > 0.3f) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * 120f * warningScale;
                int dust = Dust.NewDust(pos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
            }

            // 预警结束时生成真正的法阵
            if (Projectile.timeLeft == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<BossDragonAuthorityCircle>(), (int)Projectile.ai[0], 0f, Main.myPlayer, Projectile.ai[1]);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float effectiveRadius = 120f * warningScale;

            // 闪烁效果
            float flicker = Projectile.timeLeft < 30 ? (MathF.Sin(Projectile.timeLeft * 0.8f) * 0.4f + 0.6f) : 1f;
            float alpha = warningScale * flicker;

            // 预警圈
            int segments = 24;
            for (int i = 0; i < segments; i++) {
                float angle = runeRotation + MathHelper.TwoPi * i / segments;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;

                Color warningColor = Color.Lerp(Color.Gold, Color.Red, 0.3f) * alpha * 0.6f;
                warningColor.A = 0;

                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, warningColor,
                    angle, origin, 0.25f * warningScale, SpriteEffects.None, 0);
            }

            // 中心标记
            Color centerColor = Color.Gold * alpha * 0.5f;
            centerColor.A = 0;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, centerColor,
                runeRotation, origin, 0.5f * warningScale, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// Boss龙威法阵 - 区域控制并降下叉状天雷
    /// ai[0] = 伤害, ai[1] = 攻击阶段(0-3)
    /// </summary>
    public class BossDragonAuthorityCircle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float circleScale = 0f;
        private float runeRotation = 0f;
        private float pulsePhase = 0f;
        private int lightningTimer = 0;
        private int damageTimer = 0;

        private int AttackPhase => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 280;
            Projectile.height = 280;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360; // 6秒持续时间
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            pulsePhase += 0.08f;
            runeRotation += 0.025f;
            damageTimer++;
            lightningTimer++;

            // 法阵展开
            if (Projectile.timeLeft > 330) {
                circleScale = MathHelper.Lerp(circleScale, 1.5f, 0.06f);
            }
            // 法阵消散
            else if (Projectile.timeLeft < 50) {
                circleScale = MathHelper.Lerp(circleScale, 0f, 0.04f);
            }

            // 调整碰撞范围
            int newSize = (int)(280 * circleScale);
            Projectile.width = Projectile.height = newSize;

            // 周期性叉状天雷轰击 - 根据阶段调整频率
            int lightningInterval = Math.Max(25, 50 - AttackPhase * 8);
            if (lightningTimer >= lightningInterval && circleScale > 0.8f) {
                lightningTimer = 0;
                SummonForkedLightning();
            }

            // 周期性对范围内玩家造成伤害
            if (damageTimer >= 20 && circleScale > 0.5f) {
                damageTimer = 0;
                // 伤害逻辑由hostile=true处理
            }

            // 法阵粒子效果
            CreateCircleParticles();

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * circleScale * 0.8f);
        }

        private void SummonForkedLightning() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 在法阵内随机位置创建叉状天雷预警
            float radius = 100f * circleScale;
            Vector2 targetPos = Projectile.Center + Main.rand.NextVector2Circular(radius, radius);

            // 先创建预警（使用新的1800高度）
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos + new Vector2(0, -1800f), Vector2.Zero,
                ModContent.ProjectileType<ForkedLightningWarning>(), Projectile.damage, 0f, Main.myPlayer, targetPos.X, targetPos.Y);
        }

        private void CreateCircleParticles() {
            float effectiveRadius = 120f * circleScale;

            // 外圈旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;

                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 100, default, 1.5f * circleScale);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 4f;
            }

            // 符文闪烁
            if (Main.rand.NextBool(5)) {
                float runeAngle = runeRotation + MathHelper.TwoPi * Main.rand.Next(8) / 8f;
                Vector2 runePos = Projectile.Center + runeAngle.ToRotationVector2() * effectiveRadius * 0.7f;

                int dust = Dust.NewDust(runePos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            // 中心龙威能量
            if (Main.rand.NextBool(4)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(25 * circleScale, 25 * circleScale);
                int dust = Dust.NewDust(pos, 0, 0, DustID.GoldFlame, 0, -3f, 100, default, 2f * circleScale);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D starTex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 starOrigin = starTex.Size() / 2f;
            float effectiveRadius = 120f * circleScale;

            // 多层法阵环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = effectiveRadius * (0.5f + ring * 0.25f);
                float ringRotation = runeRotation * (ring % 2 == 0 ? 1 : -1.5f);
                int segments = 12 - ring * 2;
                float ringAlpha = (0.7f - ring * 0.15f) * circleScale;

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    float pulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                    Color runeColor = Color.Lerp(Color.Gold, Color.OrangeRed, pulse) * ringAlpha;
                    runeColor.A = 0;

                    float runeScale = (0.3f + pulse * 0.2f) * circleScale;
                    sb.Draw(starTex, pos - Main.screenPosition, null, runeColor, angle + MathHelper.PiOver4, starOrigin, runeScale, SpriteEffects.None, 0f);
                }
            }

            // 龙形中心
            DrawDragonCore(sb, effectiveRadius);

            // 外圈光环
            int borderSegments = 48;
            for (int i = 0; i < borderSegments; i++) {
                float angle = MathHelper.TwoPi * i / borderSegments;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;

                float pulse = MathF.Sin(pulsePhase * 2f + angle * 6f) * 0.3f + 0.7f;
                Color borderColor = Color.Gold * pulse * circleScale * 0.6f;
                borderColor.A = 0;

                sb.Draw(starTex, pos - Main.screenPosition, null, borderColor, angle, starOrigin, 0.2f * circleScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        private void DrawDragonCore(SpriteBatch sb, float radius) {
            Texture2D lightTex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 lightOrigin = lightTex.Size() / 2f;

            // 中心龙威光球
            float coreScale = 0.8f + MathF.Sin(pulsePhase * 1.5f) * 0.2f;
            Color coreColor = Color.Gold;
            coreColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, coreColor, 0f, lightOrigin, coreScale * circleScale, SpriteEffects.None, 0f);

            // 外层光晕
            Color haloColor = Color.OrangeRed * 0.5f;
            haloColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, haloColor, 0f, lightOrigin, 1.5f * coreScale * circleScale, SpriteEffects.None, 0f);

            // 四方龙纹
            Texture2D waveTex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 waveOrigin = new Vector2(0, waveTex.Height / 2f);

            for (int i = 0; i < 4; i++) {
                float angle = runeRotation * 2f + MathHelper.PiOver2 * i;
                Vector2 dragonPos = Projectile.Center + angle.ToRotationVector2() * radius * 0.3f;

                Color dragonColor = Color.Lerp(Color.Gold, Color.OrangeRed, MathF.Sin(pulsePhase + i) * 0.5f + 0.5f) * 0.6f * circleScale;
                dragonColor.A = 0;

                sb.Draw(waveTex, dragonPos - Main.screenPosition, null, dragonColor, angle, waveOrigin,
                    new Vector2(0.5f * circleScale, 0.15f * circleScale), SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);

            // 消散特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 最终审判 - 多道叉状天雷（使用新的1800高度）
            if (Main.netMode != NetmodeID.MultiplayerClient && circleScale > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                    Vector2 targetPos = Projectile.Center + angle.ToRotationVector2() * 100f;
                    Projectile.NewProjectile(Projectile.GetSource_Death(), targetPos + new Vector2(0, -1800f), Vector2.Zero,
                        ModContent.ProjectileType<ForkedLightningWarning>(), Projectile.damage, 0f, Main.myPlayer, targetPos.X, targetPos.Y);
                }
            }
        }
    }

    /// <summary>
    /// 叉状天雷预警 - 显示雷击即将落下的位置
    /// ai[0] = 目标X, ai[1] = 目标Y
    /// </summary>
    public class ForkedLightningWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private Vector2 targetPos;
        private float warningAlpha = 0f;
        private const float LightningHeight = 1800f; // 增加到3倍高度

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90; // 1.5秒预警时间（增加）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source) {
            targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Projectile.Center = targetPos + new Vector2(0, -LightningHeight / 2f);
        }

        public override void AI() {
            // 预警渐显（更长的渐显时间）
            if (Projectile.timeLeft > 60) {
                warningAlpha = MathHelper.Lerp(warningAlpha, 1f, 0.08f);
            }
            // 预警闪烁（更早开始闪烁）
            else if (Projectile.timeLeft < 30) {
                warningAlpha = MathF.Sin(Projectile.timeLeft * 0.5f) * 0.4f + 0.6f;
            }

            // 预警粒子（沿整个高度）
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = targetPos + new Vector2(Main.rand.NextFloat(-40, 40), Main.rand.NextFloat(-LightningHeight, 0));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 3f, 100, Color.Gold, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            // 预警结束时生成叉状天雷
            if (Projectile.timeLeft == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), targetPos + new Vector2(0, -LightningHeight), new Vector2(0, 45f),
                    ModContent.ProjectileType<ForkedCelestialLightning>(), Projectile.damage, 5f, Main.myPlayer, targetPos.X, targetPos.Y);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 预警竖线
            Color warningColor = Color.Lerp(Color.Gold, Color.Red, 0.4f) * warningAlpha * 0.6f;
            warningColor.A = 0;

            // 绘制从天而降的预警线（更长）
            Vector2 lineStart = targetPos + new Vector2(0, -LightningHeight);
            Vector2 lineEnd = targetPos;
            Vector2 direction = (lineEnd - lineStart).SafeNormalize(Vector2.UnitY);
            float distance = Vector2.Distance(lineStart, lineEnd);
            float rotation = direction.ToRotation();

            int steps = (int)(distance / 25f);
            for (int i = 0; i <= steps; i++) {
                float progress = (float)i / steps;
                Vector2 pos = Vector2.Lerp(lineStart, lineEnd, progress);

                // 闪烁效果
                float flicker = MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + i * 0.25f) * 0.3f + 0.7f;

                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, warningColor * flicker,
                    rotation, origin, new Vector2(0.18f, 1f), SpriteEffects.None, 0);
            }

            // 落点标记（更大更明显）
            Color targetColor = Color.Red * warningAlpha * 0.9f;
            targetColor.A = 0;
            float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f;
            Main.EntitySpriteDraw(tex, targetPos - Main.screenPosition, null, targetColor, 0f, origin, pulseScale * 1.2f, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 叉状天庭雷电 - 从天而降的金色叉状神雷
    /// ai[0] = 目标X, ai[1] = 目标Y
    /// </summary>
    public class ForkedCelestialLightning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private Vector2 targetPos;
        private bool hasStruck = false;
        private List<List<Vector2>> lightningBranches = [];
        private const int BranchCount = 3; // 叉状分支数

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source) {
            targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            GenerateForkedLightningPath();
        }

        private void GenerateForkedLightningPath() {
            lightningBranches.Clear();

            Vector2 startPos = Projectile.Center;

            // 主干雷电（更长的距离）
            List<Vector2> mainBranch = GenerateLightningBranch(startPos, targetPos, 45f);
            lightningBranches.Add(mainBranch);

            // 叉状分支 - 从主干中间分出（更多分支，更长）
            for (int b = 0; b < BranchCount - 1; b++) {
                int splitIndex = mainBranch.Count / 4 + Main.rand.Next(mainBranch.Count / 3);
                if (splitIndex >= mainBranch.Count) splitIndex = mainBranch.Count - 1;

                Vector2 splitPoint = mainBranch[splitIndex];
                float branchAngle = (b == 0 ? -1 : 1) * MathHelper.ToRadians(30 + Main.rand.NextFloat(20));
                Vector2 branchDirection = (targetPos - startPos).SafeNormalize(Vector2.UnitY).RotatedBy(branchAngle);
                Vector2 branchEnd = splitPoint + branchDirection * 350f; // 更长的分支

                List<Vector2> branch = GenerateLightningBranch(splitPoint, branchEnd, 25f);
                lightningBranches.Add(branch);
            }

            // 添加额外的小分支
            if (mainBranch.Count > 6) {
                int extraSplitIndex = mainBranch.Count / 2 + Main.rand.Next(mainBranch.Count / 4);
                if (extraSplitIndex < mainBranch.Count) {
                    Vector2 extraSplitPoint = mainBranch[extraSplitIndex];
                    float extraAngle = Main.rand.NextBool() ? MathHelper.ToRadians(40) : MathHelper.ToRadians(-40);
                    Vector2 extraDir = (targetPos - startPos).SafeNormalize(Vector2.UnitY).RotatedBy(extraAngle);
                    Vector2 extraEnd = extraSplitPoint + extraDir * 180f;
                    List<Vector2> extraBranch = GenerateLightningBranch(extraSplitPoint, extraEnd, 15f);
                    lightningBranches.Add(extraBranch);
                }
            }
        }

        private List<Vector2> GenerateLightningBranch(Vector2 start, Vector2 end, float maxOffset) {
            List<Vector2> points = [];
            points.Add(start);

            Vector2 direction = (end - start).SafeNormalize(Vector2.UnitY);
            float totalDistance = Vector2.Distance(start, end);
            int segments = Math.Max(3, (int)(totalDistance / 35f));

            for (int i = 1; i < segments; i++) {
                float progress = (float)i / segments;
                Vector2 basePos = Vector2.Lerp(start, end, progress);

                // 随机偏移形成闪电效果
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                float offset = Main.rand.NextFloat(-maxOffset, maxOffset) * (1f - progress * 0.5f);
                Vector2 point = basePos + perpendicular * offset;

                points.Add(point);
            }

            points.Add(end);
            return points;
        }

        public override void AI() {
            if (!hasStruck) {
                Projectile.velocity *= 1.08f;

                if (Vector2.Distance(Projectile.Center, targetPos) < 60f || Projectile.timeLeft < 55) {
                    hasStruck = true;
                    Projectile.Center = targetPos;
                    Projectile.velocity = Vector2.Zero;

                    // 雷击爆发
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 1f }, targetPos);

                    // 主落点爆发粒子
                    for (int i = 0; i < 25; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(10, 10);
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                        int dust = Dust.NewDust(targetPos, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }

                    // 分支末端也产生爆发
                    foreach (var branch in lightningBranches) {
                        if (branch.Count > 0) {
                            Vector2 endPos = branch[^1];
                            for (int i = 0; i < 10; i++) {
                                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                                int dust = Dust.NewDust(endPos, 0, 0, DustID.Electric, vel.X, vel.Y, 100, Color.Gold, 1.8f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                    }
                }
            }
            else {
                if (Projectile.timeLeft > 25) {
                    Projectile.timeLeft = 25;
                }
            }

            // 闪电粒子
            if (!hasStruck && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, 0, 0, 100, Color.Gold, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * (hasStruck ? 0.6f : 1f));

            // 分支末端也发光
            foreach (var branch in lightningBranches) {
                if (branch.Count > 0) {
                    Lighting.AddLight(branch[^1], new Vector3(1f, 0.85f, 0.3f) * 0.5f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (lightningBranches.Count == 0) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D lightTex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 lightOrigin = lightTex.Size() / 2f;

            float alpha = hasStruck ? (Projectile.timeLeft / 25f) : 1f;

            // 绘制所有分支
            for (int branchIndex = 0; branchIndex < lightningBranches.Count; branchIndex++) {
                List<Vector2> branch = lightningBranches[branchIndex];
                bool isMainBranch = branchIndex == 0;
                float branchWidth = isMainBranch ? 1f : 0.6f;

                for (int i = 0; i < branch.Count - 1; i++) {
                    Vector2 start = branch[i];
                    Vector2 end = branch[i + 1];
                    Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
                    float distance = Vector2.Distance(start, end);
                    float rotation = direction.ToRotation();

                    int steps = Math.Max(2, (int)(distance / 4f));
                    for (int j = 0; j < steps; j++) {
                        float progress = (float)j / steps;
                        Vector2 pos = Vector2.Lerp(start, end, progress);

                        // 闪电核心 - 白色
                        Color coreColor = Color.White * alpha;
                        coreColor.A = 0;
                        sb.Draw(lightTex, pos - Main.screenPosition, null, coreColor, rotation, lightOrigin, 0.25f * branchWidth, SpriteEffects.None, 0f);

                        // 内层光晕 - 金色
                        Color innerGlow = Color.Gold * 0.8f * alpha;
                        innerGlow.A = 0;
                        sb.Draw(lightTex, pos - Main.screenPosition, null, innerGlow, rotation, lightOrigin, 0.5f * branchWidth, SpriteEffects.None, 0f);

                        // 外层光晕 - 橙色
                        Color outerGlow = Color.OrangeRed * 0.4f * alpha;
                        outerGlow.A = 0;
                        sb.Draw(lightTex, pos - Main.screenPosition, null, outerGlow, rotation, lightOrigin, 0.8f * branchWidth, SpriteEffects.None, 0f);
                    }
                }
            }

            // 雷击点光球
            if (hasStruck) {
                float pulseScale = 1f + MathF.Sin(Projectile.timeLeft * 0.6f) * 0.3f;

                // 主落点
                Color strikeColor = Color.Gold * alpha;
                strikeColor.A = 0;
                sb.Draw(lightTex, targetPos - Main.screenPosition, null, strikeColor, 0f, lightOrigin, pulseScale * 1.8f, SpriteEffects.None, 0f);

                Color outerColor = Color.OrangeRed * 0.6f * alpha;
                outerColor.A = 0;
                sb.Draw(lightTex, targetPos - Main.screenPosition, null, outerColor, 0f, lightOrigin, pulseScale * 3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 检测所有分支的碰撞
            foreach (var branch in lightningBranches) {
                for (int i = 0; i < branch.Count - 1; i++) {
                    float collisionPoint = 0f;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        branch[i], branch[i + 1], 25f, ref collisionPoint)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 180);

            // 击中特效
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 100, Color.Gold, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    // ============================================================
    //  V2 天御金龙 — 天界身份机制弹幕 (替换密度档)
    // ============================================================

    /// <summary>
    /// 天命赐福区 (Mandate Zone) — 站入圈内的玩家获得天庭赐福(增伤/减伤/回蓝), 但金龙会**优先**朝赐福区
    /// 倾泻攻击 → 风险/回报取舍: 留在圈内拿 buff, 但更易被针对。纯增益, 自身不造成伤害。
    /// ai[0]=半径(像素)。地纹用 ArenaRunic(金=安全) 绘制。
    /// </summary>
    public class MandateZone : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float Radius => Projectile.ai[0] <= 0 ? 200f : Projectile.ai[0];
        private float runeRot;
        private float fade;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            runeRot += 0.01f;
            if (Projectile.timeLeft < 60)
                fade = MathHelper.Lerp(fade, 0f, 0.08f);
            else
                fade = MathHelper.Lerp(fade, 1f, 0.06f);

            float r = Radius;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                if (Vector2.DistanceSquared(p.Center, Projectile.Center) < r * r) {
                    p.AddBuff(BuffID.Endurance, 12);
                    p.AddBuff(BuffID.Wrath, 12);
                    p.AddBuff(BuffID.ManaRegeneration, 12);
                }
            }

            if (!Main.dedServ && fade > 0.3f && Main.rand.NextBool(3)) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * r;
                int d = Dust.NewDust(pos, 0, 0, DustID.GoldFlame, 0, 0, 120, default, 1.1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 1.5f;
            }
            Lighting.AddLight(Projectile.Center, 0.4f * fade, 0.35f * fade, 0.12f * fade);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || fade <= 0.01f)
                return false;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return false;

            ACMShaders.WorldDecalParams(Projectile.Center, Radius, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(fade, 0f, 1f) * 0.85f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Gold.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.Safe.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(9f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.NonPremultiplied);
            return false;
        }
    }

    /// <summary>
    /// 敕令法标 (Edict Beacon) — 敕令幕的**可破目标**: 只要场上还有法标, 它就持续向玩家降下预警雷雨;
    /// 玩家必须摧毁场地边缘的法标来终止雷雨(**目标导向, 而非耐久海绵**)。可被友方弹幕/近战击破。
    /// ai[0]=雷雨伤害; ai[1]=归属龙头 whoAmI。localAI[0]=耐久; localAI[1]=受击冷却。
    /// </summary>
    public class EdictBeacon : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public const int MaxHealth = 5;
        private const int DropFrames = 20; // 盖印落下动画帧数 (纯视觉)
        private float runeRot;
        private float pulse;
        private int age;

        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 84;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1500;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.localAI[0] = MaxHealth;
        }

        private Player NearestPlayer() {
            Player best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                float d = Vector2.DistanceSquared(p.Center, Projectile.Center);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public override void AI() {
            runeRot += 0.04f;
            pulse += 0.12f;
            age++;
            // 多人客户端 OnSpawn 不执行 → 耐久本地补初始化 (颜色反馈用; 击杀由服务器权威)
            if (age == 1 && Projectile.localAI[0] <= 0)
                Projectile.localAI[0] = MaxHealth;
            if (Projectile.localAI[1] > 0)
                Projectile.localAI[1]--;

            // 盖印落定瞬间: 冲击尘环 + 震屏 (视觉在 PreDraw 用偏移画下落)
            if (age == DropFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(3f);
                for (int i = 0; i < 14; i++) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(6, 2.5f);
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }

            // —— 可破检测: 友方弹幕重叠 或 近战挥击靠近 (盖印落定前不可破) ——
            bool struck = false;
            Rectangle box = Projectile.Hitbox;
            if (age >= DropFrames) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile op = Main.projectile[i];
                    if (!op.active || !op.friendly || op.hostile)
                        continue;
                    if (op.Hitbox.Intersects(box)) { struck = true; break; }
                }
            }
            if (!struck) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (!p.active || p.dead)
                        continue;
                    if (p.itemAnimation > 0 && p.HeldItem != null && p.HeldItem.CountsAsClass(DamageClass.Melee)
                        && Vector2.Distance(p.Center, Projectile.Center) < 70f + Projectile.width * 0.5f) {
                        struck = true;
                        break;
                    }
                }
            }

            if (struck && Projectile.localAI[1] <= 0) {
                Projectile.localAI[1] = 12;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(4, 4);
                        int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 1.4f);
                        Main.dust[d].noGravity = true;
                    }
                }
                // 双端各自扣耐久 (客户端仅用于颜色反馈); 击杀由服务器权威
                Projectile.localAI[0]--;
                if (Projectile.localAI[0] <= 0 && Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.Kill();
            }

            // —— 雷雨: 法标自身降下预警雷, 摧毁法标即减少雷雨, 全破即停 (落定后才开始) ——
            if (Main.netMode != NetmodeID.MultiplayerClient && age >= DropFrames && Projectile.timeLeft % 150 == 0) {
                Player p = NearestPlayer();
                if (p != null) {
                    Vector2 t = p.Center + Main.rand.NextVector2Circular(520f, 360f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), t + new Vector2(0, -1800f), Vector2.Zero,
                        ModContent.ProjectileType<ForkedLightningWarning>(), (int)Projectile.ai[0], 4f, Main.myPlayer, t.X, t.Y);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.55f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D star = ACMAsset.BlankStar;
            Texture2D glow = ACMAsset.SoftGlow ?? star;
            if (star == null)
                return false;

            // 盖印下落: 前 DropFrames 帧从上方 480px 以强 ease-out 砸下 (纯视觉偏移)
            float drop = MathHelper.Clamp(age / (float)DropFrames, 0f, 1f);
            float dropEase = 1f - MathF.Pow(1f - drop, 5f);
            Vector2 visCenter = Projectile.Center + new Vector2(0f, -480f * (1f - dropEase));

            // 天光柱: 落定后一根短金柱立在法标上 (标示"天规支点")
            if (drop >= 1f) {
                CelestialDragonVFX.DrawPillar(visCenter + new Vector2(0, 30f), 520f, 90f,
                    1f, 0.22f, 0.5f + MathF.Sin(pulse) * 0.08f, TelegraphColors.Holy, TelegraphColors.Gold);
            }

            float hp = MathHelper.Clamp(Projectile.localAI[0] / (float)MaxHealth, 0f, 1f);
            // 耐久越低越红(越接近破除), 给玩家进度反馈
            Color ring = Color.Lerp(TelegraphColors.Lethal, TelegraphColors.Gold, hp);
            ring.A = 0;
            float br = 1f + MathF.Sin(pulse) * 0.12f;
            Vector2 pos = visCenter - Main.screenPosition;

            Color core = TelegraphColors.Gold;
            core.A = 0;
            if (glow != null)
                Main.EntitySpriteDraw(glow, pos, null, core * 0.7f, 0f, glow.Size() / 2f, 1.2f * br, SpriteEffects.None, 0);

            int seg = 10;
            for (int i = 0; i < seg; i++) {
                float a = runeRot + MathHelper.TwoPi * i / seg;
                Vector2 rp = visCenter + a.ToRotationVector2() * 40f - Main.screenPosition;
                Main.EntitySpriteDraw(star, rp, null, ring * 0.85f, a, star.Size() / 2f, 0.3f * br, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 24; i++) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(7, 7);
                    int dt = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                    int d = Dust.NewDust(Projectile.Center, 0, 0, dt, v.X, v.Y, 100, default, 1.8f);
                    Main.dust[d].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 天龙逆鳞 (Celestial Scale) — 俯冲时沿途脱落的**无敌**金鳞。引信到点会爆裂(末段转红预警)散射金能;
    /// 若在爆裂前被友方弹幕/近战击中, 则被"反弹"为一道**无害金光束**并安全消散(玩家可主动拆弹)。
    /// ai[0]=爆裂伤害; ai[2]=1 表示已被反弹。localAI[1]=受击冷却。
    /// </summary>
    public class CelestialScale : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float rot;
        private Vector2 reflectDir = -Vector2.UnitY;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 170;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        private bool Reflected => Projectile.ai[2] == 1f;

        public override void AI() {
            rot += 0.15f;
            Projectile.velocity *= 0.94f;
            if (Projectile.localAI[1] > 0)
                Projectile.localAI[1]--;

            if (Reflected) {
                Lighting.AddLight(Projectile.Center, 0.9f, 0.8f, 0.3f);
                return; // 反弹后等待光束绘制完自然消亡
            }

            // 拆弹检测 (爆裂前可被击中反弹)
            bool struck = false;
            Rectangle box = Projectile.Hitbox;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile op = Main.projectile[i];
                if (!op.active || !op.friendly || op.hostile)
                    continue;
                if (op.Hitbox.Intersects(box)) { struck = true; break; }
            }
            if (!struck) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (!p.active || p.dead)
                        continue;
                    if (p.itemAnimation > 0 && p.HeldItem != null && p.HeldItem.CountsAsClass(DamageClass.Melee)
                        && Vector2.Distance(p.Center, Projectile.Center) < 60f) {
                        struck = true;
                        break;
                    }
                }
            }

            if (struck && Projectile.localAI[1] <= 0) {
                Projectile.ai[2] = 1f;
                reflectDir = (-Vector2.UnitY).RotatedByRandom(0.5f);
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = 16;
                Projectile.netUpdate = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f, Volume = 0.6f }, Projectile.Center);
                    ACMUtils.AddScreenShake(2f);
                }
            }

            // 引信末段转红预警
            if (Projectile.timeLeft < 40 && !Main.dedServ && Main.rand.NextBool(2)) {
                int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.RedTorch, 0, 0, 120, default, 1.3f);
                Main.dust[d].noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D star = ACMAsset.BlankStar;
            Texture2D glow = ACMAsset.SoftGlow ?? star;

            if (Reflected) {
                // 反弹: 一道无害金光束 (DrawBeam, 金=安全/权威)
                float p = MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f);
                Vector2 end = Projectile.Center + reflectDir * 700f;
                ACMShaders.DrawBeam(Projectile.Center, end, 26f * p, TelegraphColors.Gold, TelegraphColors.Holy, p, 2.0f, 2.2f);
                if (glow != null) {
                    Color c = TelegraphColors.Gold;
                    c.A = 0;
                    Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, c * p, 0f, glow.Size() / 2f, 1.4f, SpriteEffects.None, 0);
                }
                return false;
            }

            if (star == null)
                return false;
            bool armed = Projectile.timeLeft < 40;
            Color col = armed
                ? Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, 0.6f + MathF.Sin(Main.GlobalTimeWrappedHourly * 14f) * 0.3f)
                : TelegraphColors.Gold;
            col.A = 0;
            float sc = 0.32f + (armed ? MathF.Sin(Main.GlobalTimeWrappedHourly * 14f) * 0.06f : 0f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            if (glow != null)
                Main.EntitySpriteDraw(glow, pos, null, col * 0.5f, 0f, glow.Size() / 2f, 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null, col, rot, star.Size() / 2f, sc, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Reflected) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(4, 4);
                        int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 1.2f);
                        Main.dust[d].noGravity = true;
                    }
                }
                return;
            }
            // 未被拆弹 → 爆裂散射金能 (telegraph 已由红色引信给出)
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.1f, Volume = 0.7f }, Projectile.Center);
                ACMUtils.AddScreenShake(3f);
                for (int i = 0; i < 18; i++) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(8, 8);
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 1.8f);
                    Main.dust[d].noGravity = true;
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 10;
                for (int i = 0; i < count; i++) {
                    float a = MathHelper.TwoPi * i / count;
                    Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, a.ToRotationVector2() * 7f,
                        ModContent.ProjectileType<GoldenEnergy>(), (int)Projectile.ai[0], 3f, Main.myPlayer);
                }
            }
        }
    }

    // ============================================================
    //  V3 天御金龙 — 龙珠 · 天光柱阵 (充能语法招牌)
    // ============================================================

    /// <summary>
    /// 龙珠 (Celestial Dragon Pearl) — 金龙盘环时悬于环心的蓄光龙珠。
    /// 完整充能语法: 汇聚流光(密度∝sqrt(充能), 72% 硬切断) + 切向环流 → 末段静默塌缩颤闪 →
    /// 白闪释放**天光柱阵** (奇偶两波错开, 永远有安全缝) → 收尾金能环。
    /// 自身不造成伤害 (危险全部来自被预警的光柱)。
    /// ai[0]=光柱数(5/7), ai[1]=光柱伤害。
    /// </summary>
    public class CelestialDragonPearlOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ChargeTime = 110;   // 蓄光
        private const int ReleaseAt = ChargeTime;
        private const int RingAt = 200;       // 收尾金能环
        private const int LifeTime = 240;
        private const int QuietWindow = 24;   // 释放前静默塌缩

        private float bloomPulse;

        // 用本地帧龄而非 timeLeft (OnSpawn/timeLeft 不同步到多人客户端)
        private int Age => (int)Projectile.localAI[0];
        private float Charge => MathHelper.Clamp(Age / (float)ChargeTime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        private Player NearestPlayer() {
            Player best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                float d = Vector2.DistanceSquared(p.Center, Projectile.Center);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            float charge = Charge;

            if (age == 1 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 0.8f }, Projectile.Center);

            // —— 蓄光段 ——
            if (age < ChargeTime) {
                bool quiet = age > ChargeTime - QuietWindow; // 最后一口气: 静默 (发射前的吸气)
                if (!Main.dedServ && !quiet && charge < 0.72f) {
                    // 汇聚流光: 密度 ∝ sqrt(charge), 72% 处硬切断
                    if (Main.rand.NextFloat() < 0.15f + MathF.Sqrt(charge) * 0.55f) {
                        Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(160f, 420f);
                        Vector2 vel = (Projectile.Center - spawn) * 0.085f;
                        int d = Dust.NewDust(spawn, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.7f);
                        Main.dust[d].noGravity = true;
                    }
                    // 切向环流 (汇聚有旋, 不只是吸)
                    if (Main.rand.NextFloat() < charge * 0.5f) {
                        float a = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 spawn = Projectile.Center + a.ToRotationVector2() * Main.rand.NextFloat(60f, 150f);
                        Vector2 vel = a.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (2f + charge * 4f);
                        int d = Dust.NewDust(spawn, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 120, default, 1.1f);
                        Main.dust[d].noGravity = true;
                    }
                }
                // 隐雷震屏: charge² 渐强 (静默段也切断)
                if (!Main.dedServ && !quiet)
                    ACMUtils.AddScreenShake(charge * charge * 3f);

                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.4f) * (0.4f + charge * 0.8f));
                return;
            }

            // —— 释放瞬间: 白闪 + 光柱阵 ——
            if (age == ReleaseAt) {
                bloomPulse = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item68 with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);
                    ACMUtils.AddScreenShake(10f);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Player target = NearestPlayer();
                    Vector2 anchor = target?.Center ?? (Projectile.Center + new Vector2(0, 500f));
                    int count = (int)MathHelper.Clamp(Projectile.ai[0] <= 0 ? 5 : Projectile.ai[0], 3, 9);
                    const float spacing = 300f;
                    float baseY = anchor.Y + 420f;
                    int damage = (int)Projectile.ai[1];
                    // 奇数序 lane 先落, 偶数序 40f 后落 → 扫描间永远有安全缝
                    for (int i = 0; i < count; i++) {
                        float x = anchor.X + (i - (count - 1) * 0.5f) * spacing;
                        float delay = (i % 2 == 0) ? 0f : 40f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), new Vector2(x, baseY), Vector2.Zero,
                            ModContent.ProjectileType<CelestialSkyPillar>(), damage, 4f, Main.myPlayer, 45f + delay, 50f);
                    }
                }
            }

            // —— 收尾金能环 ——
            if (age == RingAt && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 10;
                for (int i = 0; i < count; i++) {
                    float a = MathHelper.TwoPi * i / count + 0.31f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, a.ToRotationVector2() * 6.5f,
                        ModContent.ProjectileType<GoldenEnergy>(), (int)(Projectile.ai[1] * 0.8f), 3f, Main.myPlayer);
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
            }

            bloomPulse = MathHelper.Lerp(bloomPulse, 0f, 0.07f);
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.55f) * MathHelper.Clamp(1.3f - (age - ReleaseAt) / 90f, 0f, 1.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            int age = Age;
            float charge = Charge;

            float radius;
            float intensity = 1f;
            if (age < ChargeTime) {
                // 半径 charge³ 生长: 无害的开局 → 骇人的收尾
                radius = 10f + 66f * charge * charge * charge;
                int quietAge = age - (ChargeTime - QuietWindow);
                if (quietAge > 0) {
                    // 塌缩颤闪: 爆发前变小 (MOTION §6)
                    float anticipation = quietAge / (float)QuietWindow;
                    float target = MathF.Cos(quietAge * 0.9f) * 0.07f + 0.42f;
                    radius *= MathHelper.SmoothStep(1f, target, anticipation);
                }
            }
            else {
                // 释放后缓慢消退
                float t = MathHelper.Clamp((age - ReleaseAt) / (float)(LifeTime - ReleaseAt), 0f, 1f);
                radius = MathHelper.Lerp(52f, 10f, t * t);
                intensity = 1f - t * 0.6f;
            }

            CelestialDragonVFX.DrawPearl(Projectile.Center, radius, charge, intensity);

            // 释放白金泛光 (走全屏名额契约)
            if (bloomPulse > 0.05f)
                ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.3f, bloomPulse, TelegraphColors.Holy, 12f, 2.2f);

            // 旋转星环 (蓄光进度外显)
            Texture2D star = ACMAsset.BlankStar;
            if (star != null && age < ChargeTime) {
                int seg = 6;
                float orbitR = radius + 26f;
                Color c = TelegraphColors.Gold;
                c.A = 0;
                for (int i = 0; i < seg; i++) {
                    float a = (float)Main.GlobalTimeWrappedHourly * (1.5f + charge * 3f) + MathHelper.TwoPi * i / seg;
                    Vector2 p = Projectile.Center + a.ToRotationVector2() * orbitR - Main.screenPosition;
                    Main.EntitySpriteDraw(star, p, null, c * (0.35f + charge * 0.5f), a, star.Size() / 2f, 0.22f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 天光柱 (Celestial Sky Pillar) — 自天顶轰落的金白光柱。
    /// ai[0]=预警帧数(红色细线), ai[1]=轰落持续帧数。伤害窗口与柱体可见期严格对齐;
    /// 光柱不追踪, 落点在生成瞬间锁定。
    /// </summary>
    public class CelestialSkyPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float PillarHeight = 1900f;
        private const float CoreWidth = 100f;
        private const int FadeFrames = 16;

        private int Telegraph => (int)MathHelper.Max(Projectile.ai[0], 10f);
        private int ActiveFrames => (int)MathHelper.Max(Projectile.ai[1], 20f);

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // OnSpawn 后按 ai 校正
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source) {
            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
        }

        // 本地帧龄 (timeLeft 不随网络同步, 多人下用 localAI 计时保证视觉/伤害窗口一致)
        private int AgeNow => (int)Projectile.localAI[0];
        private bool IsActive => AgeNow >= Telegraph && AgeNow < Telegraph + ActiveFrames;

        public override void AI() {
            Projectile.localAI[0]++;
            int age = AgeNow;

            if (age >= Telegraph + ActiveFrames + FadeFrames)
                Projectile.Kill();

            // 轰落瞬间: 光速推进 + 落点爆发
            if (age == Telegraph && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.15f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(4f);
                for (int i = 0; i < 16; i++) {
                    Vector2 v = new(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-9f, -1f));
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 2f);
                    Main.dust[d].noGravity = true;
                }
            }

            if (IsActive) {
                Lighting.AddLight(Projectile.Center, 1f, 0.9f, 0.55f);
                Lighting.AddLight(Projectile.Center + new Vector2(0, -PillarHeight * 0.5f), 0.8f, 0.7f, 0.4f);
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-CoreWidth, CoreWidth) * 0.5f, -Main.rand.NextFloat(PillarHeight * 0.85f));
                    int d = Dust.NewDust(pos, 0, 0, DustID.GoldCoin, 0, 4f, 120, default, 1.3f);
                    Main.dust[d].noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伤害窗口与视觉严格对齐: 只在轰落期造成伤害
            if (!IsActive)
                return false;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + new Vector2(0, -PillarHeight), Projectile.Center, CoreWidth * 0.55f, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            int age = AgeNow;
            Vector2 basePos = Projectile.Center + new Vector2(0, 30f);

            if (age < Telegraph) {
                // 红色细预警线 (Lethal), 末端加速闪
                float t = age / (float)Telegraph;
                float flicker = t > 0.66f ? 0.7f + MathF.Sin(age * 0.9f) * 0.3f : 1f;
                CelestialDragonVFX.DrawPillar(basePos, PillarHeight, 46f, 1f, 0.07f,
                    (0.35f + t * 0.4f) * flicker, TelegraphColors.Lethal, TelegraphColors.Lethal * 0.6f);

                // 落点标记
                Texture2D tex = ACMAsset.LightShot;
                if (tex != null) {
                    Color mark = TelegraphColors.Lethal;
                    mark.A = 0;
                    Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, mark * (0.4f + t * 0.5f),
                        0f, tex.Size() / 2f, 0.5f + t * 0.3f, SpriteEffects.None, 0);
                }
                return false;
            }

            // 轰落: 6f 内自天顶推进到底 + 满宽金白柱; 结束段淡出
            float grow = MathHelper.Clamp((age - Telegraph) / 6f, 0f, 1f);
            float fade = 1f;
            if (age >= Telegraph + ActiveFrames)
                fade = 1f - (age - Telegraph - ActiveFrames) / (float)FadeFrames;

            CelestialDragonVFX.DrawPillar(basePos, PillarHeight, CoreWidth * 2.4f, grow, 0.42f,
                fade, new Color(255, 250, 215), TelegraphColors.Gold);
            return false;
        }
    }
}

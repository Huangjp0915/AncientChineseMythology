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
    /// 路径预警特效 - 显示弹幕将要划过的范围
    /// ai[0] = 目标X, ai[1] = 目标Y
    /// </summary>
    public class CelestialPathWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private Vector2 StartPos;
        private Vector2 EndPos;

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
            }

            // 淡入淡出
            if (Projectile.timeLeft > 40)
                Projectile.alpha = (int)MathHelper.Lerp(255, 50, (60 - Projectile.timeLeft) / 20f);
            else
                Projectile.alpha = (int)MathHelper.Lerp(50, 255, (40 - Projectile.timeLeft) / 40f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (StartPos == Vector2.Zero || EndPos == Vector2.Zero) return false;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 direction = EndPos - StartPos;
            float distance = direction.Length();
            float rotation = direction.ToRotation();

            Color color = Color.Gold * (1f - Projectile.alpha / 255f) * 0.5f;
            color.A = 0;

            // 绘制路径线
            int segments = (int)(distance / 30f);
            for (int i = 0; i <= segments; i++) {
                float progress = i / (float)segments;
                Vector2 pos = Vector2.Lerp(StartPos, EndPos, progress);

                // 闪烁效果
                float flicker = 0.7f + MathF.Sin((Main.GlobalTimeWrappedHourly * 10f + i * 0.5f)) * 0.3f;

                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, color * flicker,
                    rotation, tex.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            }

            // 在两端绘制更大的标记
            Main.EntitySpriteDraw(tex, StartPos - Main.screenPosition, null, color,
                0, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, EndPos - Main.screenPosition, null, color,
                0, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);

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
}

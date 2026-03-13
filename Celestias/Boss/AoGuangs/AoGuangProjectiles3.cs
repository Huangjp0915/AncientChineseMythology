using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region 封路水龙卷

    /// <summary>
    /// 封路水龙卷 - 战场边界，使用原版龙卷纹理
    /// </summary>
    public class BarrierWaterTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float Side => ref Projectile.ai[1]; // -1左侧, 1右侧

        private float tornadoRotation;
        private float tornadoAlpha = 0f;
        private float tornadoHeight = 0f;
        private const float MaxHeight = 1200f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 99999; // 持续到Boss死亡
        }

        public override void AI() {
            // 检查Boss是否存活
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<AoGuang>()) {
                tornadoAlpha -= 0.02f;
                if (tornadoAlpha <= 0f) {
                    Projectile.Kill();
                }
                return;
            }

            // 跟随玩家位置（保持相对距离）
            Player target = Main.player[owner.target];
            if (target.active && !target.dead) {
                float targetX = target.Center.X + Side * 800f;
                Projectile.Center = new Vector2(
                    MathHelper.Lerp(Projectile.Center.X, targetX, 0.02f),
                    target.Center.Y
                );
            }

            // 逐渐显现
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.02f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.03f);
            tornadoRotation += 0.15f;

            // 龙卷粒子效果
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 40f + MathF.Abs(heightOffset / tornadoHeight) * 60f;

                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 6f, Main.rand.NextFloat(-2, 2));
                }
            }

            // 推开玩家
            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;
                float distance = MathF.Abs(player.Center.X - Projectile.Center.X);
                if (distance < 120f) {
                    float pushDirection = player.Center.X > Projectile.Center.X ? 1 : -1;
                    player.velocity.X += pushDirection * 1.5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * tornadoAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 柱形碰撞
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 60f && heightDiff < tornadoHeight / 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            // 使用原版龙卷风纹理
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = new Vector2(tornadoTex.Width / 2f, tornadoTex.Height / 2f);

            // 绘制多层龙卷风
            int segments = 162;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 2.6f + MathF.Abs(heightPercent - 0.5f) * 0.8f - seg * 0.01f;
                float segRot = tornadoRotation + seg * 0.3f;

                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                // 外层 - 青蓝色
                Color outerColor = AoGuangHelper.OceanTeal * tornadoAlpha * 0.4f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.3f, SpriteEffects.None, 0f);

                // 中层 - 龙王蓝
                Color midColor = AoGuangHelper.DragonBlue * tornadoAlpha * 0.6f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.2f, origin, segRadius, SpriteEffects.None, 0f);

                // 内层 - 水光
                Color innerColor = AoGuangHelper.WaterGlow * tornadoAlpha * 0.3f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.5f, origin, segRadius * 0.7f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 气泡弹幕

    /// <summary>
    /// 龙王气泡 - 漂浮追踪气泡
    /// </summary>
    public class DragonBubble : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float bubblePhase;
        private float bubbleScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            bubblePhase += 0.08f;
            bubbleScale = MathHelper.Lerp(bubbleScale, 1f, 0.03f);

            // 轻微追踪
            if (Projectile.timeLeft > 200) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.02f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            // 漂浮效果
            Projectile.velocity.Y += MathF.Sin(bubblePhase) * 0.05f;

            // 气泡粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Wet, 0, 0, 200, default, 0.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.WaterGlow.ToVector3() * 0.3f * bubbleScale);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(bubblePhase * 2f) * 0.1f;

            // 气泡外壳
            Color shellColor = AoGuangHelper.WaterGlow * 0.3f * bubbleScale;
            shellColor.A = 0;
            sb_Draw(Main.spriteBatch, tex, drawPos, null, shellColor, 0f, origin, 0.8f * bubbleScale * pulse, SpriteEffects.None, 0f);

            // 气泡高光 - 注意LightShot朝右，需要旋转
            Color highlightColor = AoGuangHelper.PureWhite * 0.5f * bubbleScale;
            highlightColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos + new Vector2(-8, -8) * bubbleScale, null, highlightColor,
                MathHelper.PiOver4, origin, 0.3f * bubbleScale, SpriteEffects.None, 0f);

            return false;
        }

        private void sb_Draw(SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? rect, Color color, float rot, Vector2 origin, float scale, SpriteEffects effects, float layer) {
            sb.Draw(tex, pos, rect, color, rot, origin, scale, effects, layer);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 气泡破裂
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Wet, vel.X, vel.Y, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item54 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
        }
    }

    #endregion

    #region 水柱攻击

    /// <summary>
    /// 水柱尖刺 - 从地面喷发的水柱
    /// </summary>
    public class WaterSpike : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float spikeHeight = 0f;
        private float spikeAlpha = 0f;
        private const float MaxHeight = 400f;
        private bool hasErupted = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            // 预警阶段
            if (Projectile.timeLeft > 90) {
                spikeAlpha = MathHelper.Lerp(spikeAlpha, 0.5f, 0.1f);

                // 预警粒子
                if (Main.netMode != NetmodeID.Server && Projectile.timeLeft % 3 == 0) {
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, -3f, 150, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
            // 喷发阶段
            else if (Projectile.timeLeft > 40) {
                if (!hasErupted) {
                    hasErupted = true;
                    SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f, Volume = 1f }, Projectile.Center);
                    Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 15);
                }

                float progress = 1f - (Projectile.timeLeft - 40) / 50f;
                spikeHeight = MaxHeight * ACMUtils.QuadOut(progress);
                spikeAlpha = 1f;

                // 喷发粒子
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-20, 20), -spikeHeight * Main.rand.NextFloat(0.2f, 1f));
                        int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                        int dust = Dust.NewDust(dustPos, 0, 0, dustType, Main.rand.NextFloat(-2, 2), -5f, 120, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            // 消散阶段
            else {
                spikeAlpha = Projectile.timeLeft / 40f;
            }

            Lighting.AddLight(Projectile.Center + new Vector2(0, -spikeHeight / 2), AoGuangHelper.DragonBlue.ToVector3() * spikeAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!hasErupted) return false;

            // 柱形碰撞
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            bool inHeight = targetY < Projectile.Center.Y && targetY > Projectile.Center.Y - spikeHeight;
            return distance < 30f && inHeight;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 绘制水柱 - 从下往上，旋转90度
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            for (int layer = 2; layer >= 0; layer--) {
                float layerWidth = (0.15f + layer * 0.08f) * spikeAlpha;
                float layerAlpha = 0.8f - layer * 0.2f;

                Color layerColor = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    _ => AoGuangHelper.OceanTeal
                };
                layerColor *= layerAlpha * spikeAlpha;
                layerColor.A = 0;

                // 旋转-90度使其朝上
                Vector2 scale = new Vector2(spikeHeight / tex.Width, layerWidth);
                Main.spriteBatch.Draw(tex, screenPos, null, layerColor, -MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 海啸墙

    /// <summary>
    /// 海啸墙 - 横向移动的水墙
    /// </summary>
    public class TsunamiWall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float WaveOffset => ref Projectile.ai[0];
        private float wallHeight = 200f;
        private float wavePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1500;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 200;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            wavePhase += 0.15f + WaveOffset;

            // 波浪起伏
            Projectile.position.Y += MathF.Sin(wavePhase) * 2f;

            // 水墙粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-20, 20), Main.rand.NextFloat(-wallHeight / 2, wallHeight / 2));
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, Projectile.velocity.X * 0.5f, 0, 150, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 墙形碰撞
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 40f && heightDiff < wallHeight / 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 绘制垂直水墙
            for (int layer = 2; layer >= 0; layer--) {
                float layerWidth = (0.2f + layer * 0.1f);

                Color layerColor = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    _ => AoGuangHelper.OceanTeal
                };
                layerColor *= 0.7f - layer * 0.15f;
                layerColor.A = 0;

                Vector2 scale = new Vector2(wallHeight / tex.Width, layerWidth);
                // 旋转90度使其垂直
                Main.spriteBatch.Draw(tex, screenPos, null, layerColor, -MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 龙爪斩击

    /// <summary>
    /// 龙爪斩击 - 弧形斩击弹幕
    /// </summary>
    public class DragonClawSlash : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float slashPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            slashPhase += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 斩击粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    dustPos += Main.rand.NextVector2Circular(15, 15);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.WaterGlow.ToVector3() * 0.7f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float fadeAlpha = Projectile.timeLeft / 60f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoGuangHelper.DragonBlue * progress * 0.4f * fadeAlpha;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 scale = new Vector2(0.6f * progress, 0.15f * progress);
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0f);
            }

            // 主体斩击 - LightShot朝右，这里使用GlaciateWave更适合
            Color mainColor = AoGuangHelper.WaterGlow * fadeAlpha;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.8f, 0.2f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 15; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 8);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion
}

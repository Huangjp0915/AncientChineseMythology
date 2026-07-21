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
    /// 封路水龙卷 - 战场左右边界。跟随玩家保持 ±800px, 推开越界者。
    /// Boss 进入死亡演出或消失时自动消散。
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
            // Boss 不在场 / 进入死亡演出 → 淡出消散 (淡出期不再造成伤害)
            NPC owner = Main.npc[(int)OwnerIndex];
            bool ownerValid = owner.active && owner.type == ModContent.NPCType<AoGuang>() &&
                              owner.ModNPC is AoGuang dragon && dragon.Phase != AoGuang.BossPhase.Death;
            if (!ownerValid) {
                Projectile.hostile = false;
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

            // 龙卷粒子 (量减半, 由分段绘制承担主视觉)
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
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

            // 分段龙卷 (40 段 ×2 层, 控 overdraw)
            int segments = 40;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                // 上下端略粗的沙漏形 + 分段旋转错相
                float segRadius = 2.4f + MathF.Abs(heightPercent - 0.5f) * 1.1f;
                float segRot = tornadoRotation + seg * 1.05f;
                float wobble = MathF.Sin(tornadoRotation * 1.7f + seg * 0.55f) * 14f;

                Vector2 segPos = screenPos + new Vector2(wobble, yOffset);

                Color outerColor = AoGuangHelper.OceanTeal * tornadoAlpha * 0.42f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.25f, SpriteEffects.None, 0f);

                Color midColor = AoGuangHelper.DragonBlue * tornadoAlpha * 0.55f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, -segRot * 1.2f, origin, segRadius * 0.85f, SpriteEffects.None, 0f);
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
    /// 潮涌立柱 - 从地面喷发的水柱。ai0 = 开场延时 (帧), 供整排立柱依次喷发。
    /// 节拍: 延时 → 36f 红色警戒柱 (可读) → 20f 喷发 (QuadOut 弹起) → 26f 保持 → 20f 收束。
    /// </summary>
    public class WaterSpike : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float StartDelay => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private float spikeHeight = 0f;
        private float spikeAlpha = 0f;
        private const float MaxHeight = 460f;
        private const int TelegraphTime = 36;
        private const int RiseTime = 20;
        private const int HoldTime = 26;
        private const int FadeTime = 20;
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
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Timer++;
            float t = Timer - StartDelay;

            // 延时静默期: 只有地面泡沫微涌
            if (t < 0) {
                if (Main.netMode != NetmodeID.Server && Timer % 6 == 0) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), 0), 0, 0,
                        DustID.Wet, 0, -1f, 180, default, 1.1f);
                    d.noGravity = true;
                }
                return;
            }

            if (t < TelegraphTime) {
                // 红色警戒柱期
                spikeAlpha = MathHelper.Lerp(spikeAlpha, 0.55f, 0.12f);
                if (Main.netMode != NetmodeID.Server && (int)t % 3 == 0) {
                    float h = Main.rand.NextFloat(0f, MaxHeight * 0.8f);
                    Dust d = Dust.NewDustDirect(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), -h), 0, 0,
                        DustID.RedTorch, 0, -2f, 150, TelegraphColors.Lethal, 1.4f);
                    d.noGravity = true;
                }
            }
            else if (t < TelegraphTime + RiseTime + HoldTime) {
                // 喷发 + 保持
                if (!hasErupted) {
                    hasErupted = true;
                    SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f, Volume = 1f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);
                    ACMUtils.AddScreenShake(5f);
                }

                float rise = MathHelper.Clamp((t - TelegraphTime) / RiseTime, 0f, 1f);
                spikeHeight = MaxHeight * ACMUtils.QuadOut(rise);
                spikeAlpha = 1f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-20, 20), -spikeHeight * Main.rand.NextFloat(0.2f, 1f));
                        int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, Main.rand.NextFloat(-2, 2), -5f, 120, default, 2f);
                        d.noGravity = true;
                    }
                }
            }
            else {
                // 收束
                float fade = 1f - MathHelper.Clamp((t - TelegraphTime - RiseTime - HoldTime) / FadeTime, 0f, 1f);
                spikeAlpha = fade;
                spikeHeight = MaxHeight * (0.6f + fade * 0.4f);
                if (fade <= 0f)
                    Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center + new Vector2(0, -spikeHeight / 2), AoGuangHelper.DragonBlue.ToVector3() * spikeAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伤害窗口与视觉严格对齐: 只有真正喷起的水柱才有伤害
            if (!hasErupted || spikeAlpha < 0.8f) return false;

            float distance = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            bool inHeight = targetY < Projectile.Center.Y && targetY > Projectile.Center.Y - spikeHeight;
            return distance < 30f && inHeight;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            float t = Timer - StartDelay;

            // 警戒期: 画一根 Lethal 细线柱 (形状+颜色+渐强三要素)
            if (t >= 0 && !hasErupted) {
                float warnT = MathHelper.Clamp(t / TelegraphTime, 0f, 1f);
                Color warn = TelegraphColors.Lethal * (0.25f + warnT * 0.5f);
                warn.A = 0;
                Vector2 warnScale = new Vector2(MaxHeight / tex.Width, 0.05f + warnT * 0.04f);
                Main.spriteBatch.Draw(tex, screenPos, null, warn, -MathHelper.PiOver2, origin, warnScale, SpriteEffects.None, 0f);
            }

            if (spikeHeight > 4f) {
                // 波动水柱本体: 三层 + 宽度呼吸
                float sway = 1f + MathF.Sin(Timer * 0.3f) * 0.08f;
                for (int layer = 2; layer >= 0; layer--) {
                    float layerWidth = (0.16f + layer * 0.09f) * spikeAlpha * sway;
                    float layerAlpha = 0.8f - layer * 0.2f;

                    Color layerColor = layer switch {
                        0 => AoGuangHelper.WaterGlow,
                        1 => AoGuangHelper.DragonBlue,
                        _ => AoGuangHelper.OceanTeal
                    };
                    layerColor *= layerAlpha * spikeAlpha;
                    layerColor.A = 0;

                    Vector2 scale = new Vector2(spikeHeight / tex.Width, layerWidth);
                    Main.spriteBatch.Draw(tex, screenPos, null, layerColor, -MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
                }

                // 柱顶冠花
                if (ACMAsset.LightShot != null && hasErupted) {
                    Color crown = AoGuangHelper.FoamWhite * spikeAlpha * 0.7f;
                    crown.A = 0;
                    Main.spriteBatch.Draw(ACMAsset.LightShot, screenPos + new Vector2(0, -spikeHeight), null, crown,
                        -MathHelper.PiOver2, ACMAsset.LightShot.Size() / 2f, 0.55f * spikeAlpha, SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }

    #endregion

    #region 海啸墙

    /// <summary>
    /// 海啸墙 (V3) - 一发 = 一面贯穿全场的整面浪墙, 带一个可穿越缺口。
    /// ai0 = 缺口中心世界 Y, ai1 = 缺口半宽 (px)。浪体由 AoGuangTidalWall 屏幕空间 decal 绘制:
    /// 前沿 Lethal 亮线 + 缺口 Safe 翠玉描边。成型 26f 内无伤害 (公平阀门)。
    /// </summary>
    public class TsunamiWall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float GapCenterY => ref Projectile.ai[0];
        private ref float GapHalf => ref Projectile.ai[1];

        private const float HalfHeight = 1000f; // 墙面半高 (覆盖全屏)
        private const float HalfThick = 78f;    // 浪体半厚
        private const int FormTime = 26;        // 成型时间 (无伤害)
        private const int LifeTime = 340;

        private float formProgress;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 200;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            formProgress = MathHelper.Clamp((LifeTime - Projectile.timeLeft) / (float)FormTime, 0f, 1f);

            // 末段消散
            if (Projectile.timeLeft < 30)
                formProgress = Projectile.timeLeft / 30f;

            // 浪头飞沫: 沿前沿随机溅出 (数量 ∝ 速度)
            if (Main.netMode != NetmodeID.Server && formProgress > 0.5f) {
                float dir = Projectile.velocity.X >= 0 ? 1f : -1f;
                for (int i = 0; i < 3; i++) {
                    float y = Projectile.Center.Y + Main.rand.NextFloat(-HalfHeight, HalfHeight);
                    if (MathF.Abs(y - GapCenterY) < GapHalf) continue; // 缺口处不溅
                    Vector2 dustPos = new Vector2(Projectile.Center.X + dir * HalfThick, y);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0,
                        Main.rand.NextBool() ? DustID.Water : DustID.Wet,
                        Projectile.velocity.X * 0.6f, Main.rand.NextFloat(-2f, 2f), 130, default, 1.8f);
                    d.noGravity = true;
                }
            }

            // 浪墙轰鸣 (低频循环)
            if (Projectile.timeLeft % 40 == 0 && formProgress >= 1f)
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 0.5f }, Projectile.Center);

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * formProgress);
            Lighting.AddLight(new Vector2(Projectile.Center.X, GapCenterY), TelegraphColors.Safe.ToVector3() * 0.8f * formProgress);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 成型前无伤害; 缺口内安全 (伤害区与视觉严格一致)
            if (formProgress < 1f)
                return false;
            float dx = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            if (dx > HalfThick * 0.85f)
                return false;
            float dy = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            if (dy > HalfHeight)
                return false;
            // 缺口豁免 (留 0.8 容差, 视觉缺口略大于安全区 → 宁松勿冤)
            if (MathF.Abs(targetHitbox.Center.Y - GapCenterY) < GapHalf * 0.8f)
                return false;
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            // 整面浪墙: 专属屏幕空间 decal (着色器缺失时回退为旧式贴图墙)
            if (AoGuangHelper.TidalWallEffect != null && MythologyConfig.FullscreenShadersEnabled) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                AoGuangHelper.DrawTidalWallDecal(Main.spriteBatch,
                    Projectile.Center, dir, HalfThick,
                    new Vector2(Projectile.Center.X, GapCenterY), GapHalf,
                    formProgress);
                return false;
            }

            // —— CPU 回退绘制 ——
            if (ACMAsset.GlaciateWave == null) return false;
            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            for (int layer = 2; layer >= 0; layer--) {
                Color layerColor = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    _ => AoGuangHelper.OceanTeal
                };
                layerColor *= (0.7f - layer * 0.15f) * formProgress;
                layerColor.A = 0;
                Vector2 scale = new Vector2(HalfHeight * 2f / tex.Width, 0.2f + layer * 0.1f);
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

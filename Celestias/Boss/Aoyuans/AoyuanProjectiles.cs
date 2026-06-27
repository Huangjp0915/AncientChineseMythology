using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    #region 冰弹

    /// <summary>
    /// 敖闰冰弹 - 带微弱追踪的冰霜弹幕
    /// </summary>
    public class AoyuanIceball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float icePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            icePhase += 0.12f;

            // 微弱追踪
            if (Projectile.timeLeft > 220) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.015f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 冰霜粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(icePhase * 2f) * 0.2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.DeepSeaBlue, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.5f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerColor = AoyuanHelper.DeepSeaBlue * 0.35f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // 中层
            Color midColor = AoyuanHelper.FrostCyan * 0.5f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.55f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoyuanHelper.IceCrystalWhite * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.3f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 冰柱

    /// <summary>
    /// 敖闰冰柱 - 从空中下落的冰晶
    /// </summary>
    public class AoyuanIcicle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float icePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            icePhase += 0.1f;
            Projectile.rotation += 0.1f;

            // 加速下落
            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += 0.3f;

            // 冰霜尾迹
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 10);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -2, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(icePhase * 3f) * 0.15f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.IceCrystalWhite, AoyuanHelper.DeepSeaBlue, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, (0.6f + progress * 0.4f) * pulse, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerColor = AoyuanHelper.DeepSeaBlue * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1.4f * pulse, SpriteEffects.None, 0f);

            // 中层
            Color midColor = AoyuanHelper.FrostCyan * 0.6f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoyuanHelper.IceCrystalWhite * 0.9f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 落地冰晶爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // 冰片飞溅
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 6);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch);
                d.noGravity = false;
                d.scale = 1.5f;
                d.velocity = vel;
            }
        }
    }

    #endregion

    #region 冰霜旋涡

    /// <summary>
    /// 敖闰冰霜旋涡 - 停留在原地的旋转冰霜区域
    /// </summary>
    public class AoyuanFrostVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float vortexAngle;
        private float vortexAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            vortexAngle += 0.15f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.05f);

            Projectile.velocity *= 0.95f;

            // 旋转冰霜粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 4;
                    float radius = 60f + MathF.Sin(vortexAngle * 2f + i) * 20f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            AoyuanHelper.DrawFrostAura(Main.spriteBatch, Projectile.Center, 70f, vortexAngle, vortexAlpha);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.IceTorch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 冰柱激光

    /// <summary>
    /// 敖闰冰柱激光 - 从Boss口中射出的柱状冰束大招
    /// 蓄力后释放持续冰束，缓慢追踪玩家方向扫射
    /// ai[0]: 持续时间计数, ai[1]: 目标角度
    /// </summary>
    public class AoyuanFrostBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float BeamLength = 1800f;
        private const float BeamWidth = 40f;
        private const int ChargeTime = 60;
        private const int BeamDuration = 180;

        private float beamAlpha;
        private float beamPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ChargeTime + BeamDuration;
        }

        public override void AI() {
            beamPhase += 0.1f;
            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;

            // 找到头部NPC保持位置
            bool foundOwner = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<Aoyuan>()) {
                    Projectile.Center = Main.npc[i].Center;
                    foundOwner = true;
                    break;
                }
            }
            if (!foundOwner) {
                Projectile.Kill();
                return;
            }

            if (timer < ChargeTime) {
                // 蓄力阶段 - 冰晶汇聚粒子
                beamAlpha = (float)timer / ChargeTime * 0.5f;

                // 缓慢追踪玩家
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    Projectile.ai[1] = MathHelper.Lerp(Projectile.ai[1], targetAngle, 0.08f);
                }

                if (Main.netMode != NetmodeID.Server && timer % 3 == 0) {
                    Vector2 dir = Projectile.ai[1].ToRotationVector2();
                    for (int i = 0; i < 6; i++) {
                        float dist = Main.rand.NextFloat(100, 300);
                        Vector2 offset = dir.RotatedByRandom(1.2f) * dist;
                        Vector2 dustPos = Projectile.Center + offset;
                        Vector2 dustVel = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + dist * 0.02f);
                        int d = Dust.NewDust(dustPos, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y, 150, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }

                // 蓄力音效
                if (timer == 10) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 1.5f }, Projectile.Center);
                }
            }
            else {
                // 激光阶段
                int beamTimer = timer - ChargeTime;
                float fadeIn = Math.Min(beamTimer / 15f, 1f);
                float fadeOut = Math.Min((BeamDuration - beamTimer) / 20f, 1f);
                beamAlpha = fadeIn * fadeOut;

                // 缓慢扫射追踪
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    Projectile.ai[1] = MathHelper.Lerp(Projectile.ai[1], targetAngle, 0.012f);
                }

                // 激光沿线粒子
                if (Main.netMode != NetmodeID.Server) {
                    Vector2 dir = Projectile.ai[1].ToRotationVector2();
                    for (int i = 0; i < 8; i++) {
                        float dist = Main.rand.NextFloat(0, BeamLength);
                        Vector2 dustPos = Projectile.Center + dir * dist;
                        dustPos += dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-BeamWidth * 0.5f, BeamWidth * 0.5f);
                        int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                        int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1, 100, default, 2.5f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity *= 0.3f;
                    }

                    // 起点爆花
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustVel = dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(3, 8);
                        int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.FrostStaff, dustVel.X, dustVel.Y, 100, default, 3f);
                        Main.dust[d].noGravity = true;
                    }
                }

                // 激光音效
                if (beamTimer == 0) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1.5f }, Projectile.Center);
                }

                Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 2f * beamAlpha);
            }

            Projectile.rotation = Projectile.ai[1];
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;
            if (timer < ChargeTime) return false;

            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * BeamLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamWidth * 0.6f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (beamAlpha <= 0.01f) return false;

            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            float rot = Projectile.ai[1] + MathHelper.PiOver2;

            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;
            bool isCharging = timer < ChargeTime;

            if (isCharging) {
                // 蓄力光球
                float chargeProgress = (float)timer / ChargeTime;
                float pulse = 1f + MathF.Sin(beamPhase * 4f) * 0.3f;

                Color chargeColor = AoyuanHelper.FrostCyan * chargeProgress * 0.6f * pulse;
                chargeColor.A = 0;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(tex, drawPos, null, chargeColor, 0f, origin, 2f * chargeProgress * pulse, SpriteEffects.None, 0f);

                Color innerColor = AoyuanHelper.IceCrystalWhite * chargeProgress * 0.4f;
                innerColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, innerColor, 0f, origin, 1f * chargeProgress, SpriteEffects.None, 0f);
            }
            else {
                // 激光柱绘制 - 沿射线方向铺设多层光点
                float pulse = 1f + MathF.Sin(beamPhase * 3f) * 0.15f;
                float segmentStep = 30f;
                int segments = (int)(BeamLength / segmentStep);

                for (int layer = 2; layer >= 0; layer--) {
                    float layerScale;
                    Color layerColor;
                    switch (layer) {
                        case 2:
                            layerScale = 2.8f * pulse;
                            layerColor = AoyuanHelper.DeepSeaBlue * 0.2f * beamAlpha;
                            break;
                        case 1:
                            layerScale = 1.8f * pulse;
                            layerColor = AoyuanHelper.FrostCyan * 0.5f * beamAlpha;
                            break;
                        default:
                            layerScale = 0.9f;
                            layerColor = AoyuanHelper.IceCrystalWhite * 0.8f * beamAlpha;
                            break;
                    }
                    layerColor.A = 0;

                    for (int s = 0; s < segments; s++) {
                        Vector2 segPos = Projectile.Center + dir * (s * segmentStep) - Main.screenPosition;
                        float wave = MathF.Sin(beamPhase * 2f + s * 0.3f + layer) * 3f;
                        segPos += dir.RotatedBy(MathHelper.PiOver2) * wave;
                        Main.spriteBatch.Draw(tex, segPos, null, layerColor, rot, origin, layerScale, SpriteEffects.None, 0f);
                    }
                }

                // 起点高亮光球
                Vector2 startDraw = Projectile.Center - Main.screenPosition;
                Color startGlow = AoyuanHelper.IceCrystalWhite * beamAlpha;
                startGlow.A = 0;
                Main.spriteBatch.Draw(tex, startDraw, null, startGlow, 0f, origin, 3f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            for (int i = 0; i < 40; i++) {
                float dist = Main.rand.NextFloat(0, 400);
                Vector2 dustPos = Projectile.Center + dir * dist + Main.rand.NextVector2Circular(30, 30);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    #endregion

    #region 永冻地痕 - 签名机制（无伤害，叠加冰冻/打滑）

    /// <summary>
    /// 敖闰永冻地痕 - 敖闰巡游时留下的寒冰区域
    /// 玩家站在地痕上叠加冰冻层（减速→3层冻结约1秒）；二阶段地痕额外令地面打滑
    /// ai[0]: 是否二阶段地痕（0/1）
    /// </summary>
    public class AoyuanPermafrostTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float Radius = 70f;
        private float trailPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = (int)(Radius * 2);
            Projectile.height = (int)(Radius * 2);
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        private bool IsPhase2 => Projectile.ai[0] == 1f;

        public override void AI() {
            trailPhase += 0.05f;
            Projectile.velocity = Vector2.Zero;

            // 客户端各自对本地玩家施加冰冻 / 打滑
            if (!VaultUtils.isServer) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && Vector2.Distance(lp.Center, Projectile.Center) < Radius) {
                    var fp = lp.GetModPlayer<AoyuanFrostPlayer>();
                    fp.AddChill();
                    if (IsPhase2)
                        fp.slipperyTimer = System.Math.Max(fp.slipperyTimer, 30);
                }

                if (Main.rand.NextBool(5)) {
                    Vector2 dp = Projectile.Center + Main.rand.NextVector2Circular(Radius, Radius * 0.5f);
                    var d = Dust.NewDustPerfect(dp, DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1f + Main.rand.NextFloat(0.6f);
                    d.velocity = new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f));
                }
            }

            float fade = System.Math.Min(Projectile.timeLeft / 60f, 1f);
            Lighting.AddLight(Projectile.Center, AoyuanHelper.DeepSeaBlue.ToVector3() * 0.25f * fade);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow;
            if (tex == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float fadeIn = System.Math.Min((300 - Projectile.timeLeft) / 20f, 1f);
            float fadeOut = System.Math.Min(Projectile.timeLeft / 60f, 1f);
            float alpha = fadeIn * fadeOut;
            float pulse = 1f + MathF.Sin(trailPhase * 2f) * 0.1f;

            Color baseColor = (IsPhase2 ? AoyuanHelper.WestSeaTeal : AoyuanHelper.DeepSeaBlue) * 0.35f * alpha;
            baseColor.A = 0;
            float patchScale = (Radius / (tex.Width * 0.5f)) * 1.6f * pulse;
            Main.spriteBatch.Draw(tex, drawPos, null, baseColor, 0f, origin, new Vector2(patchScale, patchScale * 0.55f), SpriteEffects.None, 0f);

            Color rim = AoyuanHelper.FrostCyan * 0.25f * alpha;
            rim.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, rim, 0f, origin, new Vector2(patchScale * 0.6f, patchScale * 0.33f), SpriteEffects.None, 0f);

            return false;
        }
    }

    #endregion

    #region 冰晶棋局 - 预告冰柱落点

    /// <summary>
    /// 敖闰冰晶棋局预告 - 标记一个落点，仅"真柱"会落下冰柱
    /// ai[0]: 是否真柱（1=会落冰）
    /// </summary>
    public class AoyuanPillarTelegraph : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WarnTime = 48;
        private float telePhase;
        private bool spawned;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + 6;
        }

        private bool IsReal => Projectile.ai[0] == 1f;

        public override void AI() {
            telePhase += 0.12f;
            Projectile.velocity = Vector2.Zero;

            int elapsed = (WarnTime + 6) - Projectile.timeLeft;

            if (IsReal && !spawned && elapsed >= WarnTime) {
                spawned = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 from = Projectile.Center - new Vector2(0, 700f);
                    int p = Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        from,
                        new Vector2(0, 16f),
                        ModContent.ProjectileType<AoyuanIcicle>(),
                        (int)Projectile.damage,
                        Projectile.knockBack,
                        Main.myPlayer);
                    Main.projectile[p].tileCollide = true;
                }
                if (!VaultUtils.isServer)
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
            }

            if (!VaultUtils.isServer && IsReal && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(18, 18), DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = new Vector2(0, -1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = ACMAsset.SoftGlow;
            Texture2D wave = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            int elapsed = (WarnTime + 6) - Projectile.timeLeft;
            float urgency = System.Math.Clamp((float)elapsed / WarnTime, 0f, 1f);
            float pulse = 1f + MathF.Sin(telePhase * (2f + urgency * 5f)) * 0.3f;

            // 真柱：明亮冰白警示圈；虚招：暗淡（冰系预警走统一 TelegraphColors 冰蓝, 不与"红=致命"冲突）
            Color markColor = IsReal
                ? Color.Lerp(TelegraphColors.Frost, TelegraphColors.IceWhite, urgency) * (0.4f + urgency * 0.5f)
                : TelegraphColors.DeepFrost * 0.18f;
            markColor.A = 0;

            if (glow != null) {
                Vector2 origin = glow.Size() / 2f;
                float scale = (IsReal ? 0.7f : 0.45f) * pulse;
                Main.spriteBatch.Draw(glow, drawPos, null, markColor, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            // 真柱额外画一道下落预告竖光（GlaciateWave 旋转竖立）
            if (IsReal && wave != null) {
                Vector2 wo = wave.Size() / 2f;
                Color beam = TelegraphColors.Frost * (0.15f + urgency * 0.35f);
                beam.A = 0;
                Main.spriteBatch.Draw(wave, drawPos - new Vector2(0, 120), null, beam, MathHelper.PiOver2,
                    wo, new Vector2(0.5f * urgency, 0.18f), SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 暴雪帷幕 - 推进雪墙带缺口

    /// <summary>
    /// 敖闰暴雪帷幕 - 横向推进的雪墙，墙上留一道上下移动的缺口
    /// ai[0]: 推进方向(±1)  ai[1]: 缺口中心相对偏移
    /// </summary>
    public class AoyuanBlizzardWall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float HalfHeight = 850f;
        private const float HalfWidth = 45f;
        private float wallPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        private float GapHalf => Main.expertMode ? 100f : 130f;
        private float GapCenterY => Projectile.Center.Y + Projectile.ai[1] + MathF.Sin(wallPhase * 0.8f) * 170f;

        public override void AI() {
            wallPhase += 0.05f;

            if (!VaultUtils.isServer) {
                float gy = GapCenterY;
                for (int i = 0; i < 3; i++) {
                    float yy = Projectile.Center.Y + Main.rand.NextFloat(-HalfHeight, HalfHeight);
                    if (System.Math.Abs(yy - gy) < GapHalf) continue;
                    Vector2 dp = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfWidth, HalfWidth), yy);
                    var d = Dust.NewDustPerfect(dp, Main.rand.NextBool() ? DustID.IceTorch : DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 1.6f + Main.rand.NextFloat(0.6f);
                    d.velocity = new Vector2(Projectile.velocity.X * 0.3f, Main.rand.NextFloat(-1f, 1f));
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.3f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 c = targetHitbox.Center.ToVector2();
            if (System.Math.Abs(c.X - Projectile.Center.X) > HalfWidth) return false;
            if (c.Y < Projectile.Center.Y - HalfHeight || c.Y > Projectile.Center.Y + HalfHeight) return false;
            // 缺口内安全
            if (System.Math.Abs(c.Y - GapCenterY) < GapHalf) return false;
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow;
            if (tex == null) return false;
            Vector2 origin = tex.Size() / 2f;
            float gy = GapCenterY;

            int segments = 26;
            for (int s = 0; s <= segments; s++) {
                float yy = Projectile.Center.Y - HalfHeight + (2 * HalfHeight) * s / segments;
                if (System.Math.Abs(yy - gy) < GapHalf) continue;
                Vector2 dp = new Vector2(Projectile.Center.X, yy) - Main.screenPosition;
                Color c = AoyuanHelper.FrostCyan * 0.4f;
                c.A = 0;
                Main.spriteBatch.Draw(tex, dp, null, c, 0f, origin, new Vector2(1.1f, 1.4f), SpriteEffects.None, 0f);
                Color core = AoyuanHelper.IceCrystalWhite * 0.3f;
                core.A = 0;
                Main.spriteBatch.Draw(tex, dp, null, core, 0f, origin, new Vector2(0.5f, 0.7f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion

    #region 绝对零度 - 放射冻结波

    /// <summary>
    /// 敖闰绝对零度放射波 - 从 Boss 向外扩散的冻结环
    /// ai[0]: 是否被打断(1=削弱版，仅减速；0=完整冻结)
    /// 各客户端在环波经过本地玩家时施加冻结/冰冻
    /// </summary>
    public class AoyuanAbsoluteZeroBurst : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxRadius = 1500f;
        private const int Lifetime = 70;
        private const float Band = 90f;

        private float burstPhase;
        private bool appliedLocal;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lifetime;
        }

        private bool Broken => Projectile.ai[0] == 1f;
        private float Radius => MaxRadius * (1f - Projectile.timeLeft / (float)Lifetime);

        public override void AI() {
            burstPhase += 0.2f;
            Projectile.velocity = Vector2.Zero;

            float radius = Radius;

            // 本地玩家被环波扫到 → 冻结 / 减速
            if (!VaultUtils.isServer && !appliedLocal) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead) {
                    float dist = Vector2.Distance(lp.Center, Projectile.Center);
                    if (System.Math.Abs(dist - radius) < Band) {
                        appliedLocal = true;
                        var fp = lp.GetModPlayer<AoyuanFrostPlayer>();
                        if (Broken) {
                            fp.AddChill();
                        }
                        else {
                            fp.frozenTimer = System.Math.Max(fp.frozenTimer, 70);
                        }
                    }
                }
            }

            if (!VaultUtils.isServer) {
                int count = System.Math.Max((int)(radius / 40f), 8);
                for (int i = 0; i < count; i++) {
                    float ang = MathHelper.TwoPi * i / count + burstPhase * 0.2f;
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                    if (!Main.rand.NextBool(3)) continue;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = ang.ToRotationVector2() * 4f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 1.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return System.Math.Abs(dist - Radius) < Band;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow;
            if (tex == null) return false;
            Vector2 origin = tex.Size() / 2f;
            float radius = Radius;
            float fade = System.Math.Min(Projectile.timeLeft / 25f, 1f);

            int ringCount = System.Math.Max((int)(radius / 28f), 12);
            // 被打断(broken)=削弱版仅减速 → 冰蓝(非致命); 完整冻结=致命 → 纯红(全局契约: 红只留给真正致命伤害源)
            Color c = (Broken ? TelegraphColors.Frost : TelegraphColors.Lethal) * 0.55f * fade;
            c.A = 0;
            for (int i = 0; i < ringCount; i++) {
                float ang = MathHelper.TwoPi * i / ringCount;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, c, 0f, origin, 0.7f, SpriteEffects.None, 0f);
            }
            Color core = AoyuanHelper.IceCrystalWhite * 0.35f * fade;
            core.A = 0;
            for (int i = 0; i < ringCount; i++) {
                float ang = MathHelper.TwoPi * i / ringCount + 0.1f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, core, 0f, origin, 0.35f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion
}

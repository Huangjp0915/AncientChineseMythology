using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    #region 1. 闪电节点 - 雷链穿刺核心弹幕

    /// <summary>
    /// 闪电节点 - 固定在空中，延迟后与相邻节点间产生电弧伤害
    /// ai[0] = 节点编号, ai[1] = 总节点数
    /// 机制：节点存活期间，每隔一段时间对附近玩家/节点间区域放电
    /// </summary>
    public class AoshunLightningNode : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float nodePhase;
        private bool activated;
        private const int ActivationDelay = 40;
        private const int Duration = 180;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ActivationDelay + Duration;
        }

        public override void AI() {
            nodePhase += 0.08f;
            Projectile.velocity = Vector2.Zero;

            int timer = (ActivationDelay + Duration) - Projectile.timeLeft;

            if (timer < ActivationDelay) {
                // 预警阶段 - 闪烁增强
                if (!VaultUtils.isServer && timer % 6 == 0) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                        DustID.Electric);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = Main.rand.NextVector2Circular(1, 1);
                }
            }
            else {
                activated = true;

                // 激活后持续放电 - 寻找相邻节点并绘制电弧
                if (!VaultUtils.isServer && timer % 8 == 0) {
                    // 电弧粒子
                    FindAndArcToNeighbors();
                }
            }

            float breathe = 0.5f + MathF.Sin(nodePhase * 2f) * 0.3f;
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * breathe);
        }

        private void FindAndArcToNeighbors() {
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;

            // 查找相邻节点
            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == totalNodes) {
                    // 在两节点间生成电弧粒子
                    Vector2 start = Projectile.Center;
                    Vector2 end = p.Center;
                    int steps = (int)(Vector2.Distance(start, end) / 20f);
                    for (int s = 0; s < steps; s++) {
                        float t = (float)s / steps;
                        Vector2 pos = Vector2.Lerp(start, end, t);
                        pos += Main.rand.NextVector2Circular(10, 10);
                        var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = Main.rand.NextVector2Circular(1, 1);
                    }
                    break;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!activated) return false;

            // 节点本体碰撞
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            if (Vector2.Distance(Projectile.Center, targetCenter) < 50f)
                return true;

            // 与相邻节点的连线碰撞
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == totalNodes) {
                    float point = 0f;
                    if (Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(), targetHitbox.Size(),
                        Projectile.Center, p.Center, 20f, ref point))
                        return true;
                    break;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(nodePhase * 3f) * 0.25f;
            float alpha = activated ? 0.8f : (0.3f + MathF.Sin(nodePhase * 5f) * 0.2f);

            // 外层光晕
            Color outerColor = AoshunHelper.ThunderPurple * 0.3f * alpha * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1.2f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoshunHelper.ElectricWhite * 0.7f * alpha;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            // 如果激活且有相邻节点，绘制连线
            if (activated) {
                DrawArcToNeighbor(tex, origin);
            }

            return false;
        }

        private void DrawArcToNeighbor(Texture2D tex, Vector2 origin) {
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == totalNodes) {
                    // 在两点间绘制光点连线
                    Vector2 start = Projectile.Center - Main.screenPosition;
                    Vector2 end = p.Center - Main.screenPosition;
                    int segments = (int)(Vector2.Distance(start, end) / 25f);
                    float flicker = MathF.Sin(nodePhase * 4f + Projectile.whoAmI) * 0.3f;

                    for (int s = 0; s <= segments; s++) {
                        float t = (float)s / Math.Max(segments, 1);
                        Vector2 pos = Vector2.Lerp(start, end, t);
                        // 锯齿偏移
                        float zigzag = MathF.Sin(t * MathF.PI * 4f + nodePhase * 3f) * 12f;
                        Vector2 perp = (end - start).SafeNormalize(Vector2.UnitY);
                        perp = new Vector2(-perp.Y, perp.X);
                        pos += perp * zigzag;

                        Color arcColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, 0.5f + flicker);
                        arcColor *= 0.5f;
                        arcColor.A = 0;
                        Main.spriteBatch.Draw(tex, pos, null, arcColor, 0f, origin, 0.3f, SpriteEffects.None, 0f);
                    }
                    break;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }

    #endregion

    #region 2. 龙鳞弹幕 - 龙鳞风暴的投射物

    /// <summary>
    /// 带电龙鳞 - 从蠕虫身体段抛射，带重力和弹跳
    /// 碰到地面不消失而是弹跳一次并留下短暂电场
    /// </summary>
    public class AoshunDragonScale : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int bounceCount;
        private float scalePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            scalePhase += 0.15f;

            // 带重力
            Projectile.velocity.Y += 0.15f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            // 电弧尾迹
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.NorthSeaCyan.ToVector3() * 0.4f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bounceCount++;
            if (bounceCount >= 2) return true;

            // 弹跳
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 1f)
                Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 1f)
                Projectile.velocity.X = -oldVelocity.X * 0.5f;

            // 弹跳时放出电火花
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(3, 3);
                    d.scale = 1.5f;
                }
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(scalePhase * 2f) * 0.15f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.NorthSeaCyan, AoshunHelper.ThunderPurple, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.35f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 核心
            Color coreColor = AoshunHelper.NorthSeaCyan * 0.7f * pulse;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin, 0.45f * pulse, SpriteEffects.None, 0f);

            Color innerColor = AoshunHelper.ElectricWhite * 0.6f;
            innerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, innerColor, Projectile.rotation, origin, 0.25f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 3. 龙卷风 - 龙卷缠绕的追踪弹幕

    /// <summary>
    /// 龙卷风弹幕 - 缓慢追踪玩家，大范围碰撞，带击退
    /// 存活期间持续产生旋风粒子和吸引效果
    /// </summary>
    public class AoshunTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float spinAngle;
        private float tornadoAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
        }

        public override void AI() {
            spinAngle += 0.2f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.03f);

            // 缓慢追踪玩家
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.02f);
                float speed = Math.Min(Projectile.velocity.Length(), 4f);
                if (speed < 2f) speed = 2f;
                Projectile.velocity = newAngle.ToRotationVector2() * speed;
            }

            // 轻微吸引附近玩家
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                float dist = Vector2.Distance(Projectile.Center, p.Center);
                if (dist < 200f && dist > 30f) {
                    Vector2 pull = (Projectile.Center - p.Center).SafeNormalize(Vector2.Zero) * 0.3f;
                    p.velocity += pull;
                }
            }

            // 旋风粒子
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    float angle = spinAngle + MathHelper.TwoPi * i / 3;
                    float radius = 30f + MathF.Sin(spinAngle + i) * 15f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    dustPos.Y += Main.rand.NextFloat(-40, 0); // 向上延伸

                    int dustType = Main.rand.NextBool(3) ? DustID.Electric : DustID.Cloud;
                    var d = Dust.NewDustPerfect(dustPos, dustType);
                    d.noGravity = true;
                    d.scale = 1.5f + Main.rand.NextFloat(0.5f);
                    d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f + new Vector2(0, -2);
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.StormGray.ToVector3() * 0.5f * tornadoAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < 55f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 多层旋转光效模拟龙卷
            for (int ring = 0; ring < 4; ring++) {
                float ringY = -ring * 20f;
                float ringScale = 0.8f - ring * 0.12f;
                float ringRot = spinAngle * (1f + ring * 0.4f);
                int particles = 6;

                for (int i = 0; i < particles; i++) {
                    float angle = ringRot + MathHelper.TwoPi * i / particles;
                    float radius = (30f - ring * 5f) * ringScale;
                    Vector2 pos = drawPos + angle.ToRotationVector2() * radius + new Vector2(0, ringY);

                    Color color = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.LightningBlue, ring / 3f);
                    color *= tornadoAlpha * (0.5f - ring * 0.08f);
                    color.A = 0;
                    Main.spriteBatch.Draw(tex, pos, null, color, 0f, origin, ringScale * 0.5f, SpriteEffects.None, 0f);
                }
            }

            // 底部涡流
            Color vortexColor = AoshunHelper.ThunderPurple * 0.3f * tornadoAlpha;
            vortexColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, vortexColor, spinAngle, origin, 0.9f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 5f;
                d.scale = 2f;
            }
        }
    }

    #endregion

    #region 4. 天雷印 - 延迟落雷标记

    /// <summary>
    /// 天雷印标记 - 固定在地面/空中，倒计时后引爆为巨大雷击柱
    /// ai[0] = 延迟帧数（倒计时）
    /// 预警阶段显示地面电弧标记，到时间后变为高伤雷柱
    /// </summary>
    public class AoshunThunderSeal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float sealPhase;
        private bool detonated;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false; // 预警阶段不伤害
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; // 会在AI中自我管理
        }

        public override void AI() {
            sealPhase += 0.1f;
            Projectile.velocity = Vector2.Zero;

            Projectile.ai[0]--;

            if (Projectile.ai[0] > 0) {
                // 预警阶段 - 电弧标记越来越密集
                float urgency = 1f - Projectile.ai[0] / 90f; // 原延迟比例
                urgency = Math.Clamp(urgency, 0f, 1f);

                if (!VaultUtils.isServer) {
                    int particleCount = (int)(urgency * 6) + 1;
                    for (int i = 0; i < particleCount; i++) {
                        Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(30 + urgency * 20, 30 + urgency * 20);
                        var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                        d.noGravity = true;
                        d.scale = 1f + urgency;
                        d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + urgency * 3f);
                    }
                }

                // 倒计时音效
                if (Projectile.ai[0] == 10) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
                }

                Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.4f * urgency);
            }
            else if (!detonated) {
                // 引爆！
                detonated = true;
                Projectile.hostile = true;
                Projectile.width = 80;
                Projectile.height = 800;
                Projectile.position.X -= 20; // 居中扩展后的碰撞箱
                Projectile.position.Y -= 700; // 雷柱向上延伸
                Projectile.timeLeft = 15; // 短暂存在

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.5f }, Projectile.Center);

                // 爆发粒子
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 30; i++) {
                        Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-40, 40), Main.rand.NextFloat(-400, 0));
                        int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                        var d = Dust.NewDustPerfect(dustPos, dustType);
                        d.noGravity = true;
                        d.scale = 2.5f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-8, -2));
                    }

                    // 地面碎裂粒子
                    for (int i = 0; i < 10; i++) {
                        Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-50, 50), 0);
                        var d = Dust.NewDustPerfect(pos, DustID.Smoke);
                        d.noGravity = false;
                        d.scale = 2f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-6, -1));
                    }
                }

                Lighting.AddLight(Projectile.Center, AoshunHelper.ElectricWhite.ToVector3() * 2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (!detonated) {
                // 预警标记 - 地面旋转电弧圈
                float urgency = Math.Clamp(1f - Projectile.ai[0] / 90f, 0f, 1f);
                float pulse = 1f + MathF.Sin(sealPhase * 4f) * 0.3f * urgency;

                Color markColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, urgency) * (0.3f + urgency * 0.5f);
                markColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, markColor, sealPhase * 2f, origin, (0.8f + urgency * 0.5f) * pulse, SpriteEffects.None, 0f);

                // 内圈
                Color innerColor = AoshunHelper.ThunderPurple * (0.2f + urgency * 0.3f);
                innerColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, innerColor, -sealPhase * 3f, origin, 0.4f * pulse, SpriteEffects.None, 0f);
            }
            else {
                // 雷柱 - 从标记点向上延伸的光柱
                float fade = Projectile.timeLeft / 15f;

                for (int y = 0; y < 20; y++) {
                    Vector2 pillarPos = drawPos + new Vector2(0, -y * 40f);
                    float wave = MathF.Sin(sealPhase * 3f + y * 0.5f) * 8f;
                    pillarPos.X += wave;

                    Color pillarColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, y / 20f);
                    pillarColor *= fade * 0.6f;
                    pillarColor.A = 0;
                    Main.spriteBatch.Draw(tex, pillarPos, null, pillarColor, 0f, origin, new Vector2(1.5f, 2f), SpriteEffects.None, 0f);
                }

                // 底部爆发光
                Color burstColor = AoshunHelper.ElectricWhite * fade;
                burstColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, burstColor, 0f, origin, 3f * fade, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 5. 冲击波 - 环形扩散弹幕

    /// <summary>
    /// 冲击波 - 从Boss向外扩散的环形弹幕
    /// 用于深渊伏击和龙王怒啸
    /// </summary>
    public class AoshunShockwave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wavePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            wavePhase += 0.1f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 逐渐减速
            Projectile.velocity *= 0.98f;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), dustType);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(wavePhase * 3f) * 0.2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoshunHelper.LightningBlue * progress * 0.3f;
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.6f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            Color coreColor = AoshunHelper.ElectricWhite * 0.7f * pulse;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 6. 风暴之眼 - 缩小安全区

    /// <summary>
    /// 风暴之眼 - 以固定位置为中心的缩小安全区
    /// ai[0] = 总持续时间
    /// 安全区内部不伤害，外部持续伤害
    /// 半径从700逐渐缩小到200，然后消散
    /// </summary>
    public class AoshunStormEye : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxRadius = 700f;
        private const float MinRadius = 200f;

        private float stormPhase;
        private float currentRadius;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false; // 手动碰撞检测
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            stormPhase += 0.05f;
            Projectile.velocity = Vector2.Zero;

            int totalDuration = (int)Projectile.ai[0];
            if (totalDuration <= 0) totalDuration = 240;
            int elapsed = totalDuration - Projectile.timeLeft + (400 - totalDuration);

            // 半径缩小曲线
            float progress = Math.Clamp((float)elapsed / totalDuration, 0f, 1f);
            currentRadius = MathHelper.Lerp(MaxRadius, MinRadius, AoshunHelper.SineInOut(progress));

            // 对安全区外的玩家造成伤害
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                float dist = Vector2.Distance(Projectile.Center, p.Center);
                if (dist > currentRadius) {
                    // 惩罚伤害 - 越远越痛
                    float overflowRatio = (dist - currentRadius) / 200f;
                    overflowRatio = Math.Clamp(overflowRatio, 0f, 1f);
                    int dmg = (int)(10 + overflowRatio * 30);
                    if (Main.GameUpdateCount % 15 == 0) {
                        p.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                            p.name + " 被北海风暴吞噬"),
                            dmg, 0);
                    }

                    // 推向中心的力
                    Vector2 push = (Projectile.Center - p.Center).SafeNormalize(Vector2.Zero) * 0.5f;
                    p.velocity += push;
                }
            }

            // 风暴壁粒子
            if (!VaultUtils.isServer) {
                int particleCount = (int)(currentRadius / 30f);
                for (int i = 0; i < particleCount; i++) {
                    float angle = stormPhase * 2f + MathHelper.TwoPi * i / particleCount;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * currentRadius;
                    dustPos += Main.rand.NextVector2Circular(15, 15);

                    int dustType = Main.rand.NextBool(3) ? DustID.Electric : DustID.Cloud;
                    var d = Dust.NewDustPerfect(dustPos, dustType);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f);

            // 超时销毁
            if (Projectile.timeLeft <= 400 - totalDuration) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 绘制风暴壁（多层环形光点）
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = currentRadius + ring * 15f;
                float ringAlpha = 0.4f - ring * 0.1f;
                int pointCount = (int)(ringRadius / 20f);
                float ringRot = stormPhase * (2f + ring * 0.5f) * (ring % 2 == 0 ? 1 : -1);

                for (int i = 0; i < pointCount; i++) {
                    float angle = ringRot + MathHelper.TwoPi * i / pointCount;
                    Vector2 pos = drawPos + angle.ToRotationVector2() * ringRadius;

                    Color color = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.LightningBlue, ring / 2f);
                    color *= ringAlpha;
                    color.A = 0;
                    Main.spriteBatch.Draw(tex, pos, null, color, 0f, origin, 0.4f - ring * 0.08f, SpriteEffects.None, 0f);
                }
            }

            // 中心安全区指示
            Color safeColor = AoshunHelper.NorthSeaCyan * 0.1f;
            safeColor.A = 0;
            float safeScale = currentRadius / (tex.Width * 0.5f);
            Main.spriteBatch.Draw(tex, drawPos, null, safeColor, 0f, origin, safeScale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            // 消散粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * currentRadius;
                var d = Dust.NewDustPerfect(pos, DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 8f;
                d.scale = 2.5f;
            }
        }
    }

    #endregion

    #region 7. 电痕 - 雷霆连环冲留下的持续伤害

    /// <summary>
    /// 持续电痕 - 留在地面的静态伤害区域
    /// 持续4秒，接触即伤
    /// </summary>
    public class AoshunElectricTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float trailPhase;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            trailPhase += 0.08f;
            Projectile.velocity = Vector2.Zero;

            // 电弧粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 15);
                var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1f + Main.rand.NextFloat(0.5f);
                d.velocity = Main.rand.NextVector2Circular(1, 1);
            }

            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f * fade);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            float pulse = 1f + MathF.Sin(trailPhase * 4f) * 0.2f;

            // 底层光晕
            Color baseColor = AoshunHelper.ThunderPurple * 0.2f * fade;
            baseColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, baseColor, trailPhase, origin, 0.8f * pulse, SpriteEffects.None, 0f);

            // 上层电弧
            Color arcColor = AoshunHelper.LightningBlue * 0.4f * fade * pulse;
            arcColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, arcColor, -trailPhase * 1.5f, origin, 0.5f, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoshunHelper.ElectricWhite * 0.3f * fade;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.25f, SpriteEffects.None, 0f);

            return false;
        }
    }

    #endregion
}

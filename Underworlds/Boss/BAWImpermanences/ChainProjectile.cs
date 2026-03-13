using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑无常基础锁链弹幕 - 带镰刀头的飞行锁链
    /// </summary>
    public class ChainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        // 存储锁链起点（发射者位置）
        private Vector2 chainOrigin;
        private bool initialized = false;
        private float spinSpeed = 0.3f;
        private float glowPulse = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 0;
        }

        public override void AI() {
            // 初始化锁链起点
            if (!initialized) {
                chainOrigin = Projectile.Center;
                initialized = true;
            }

            // 镰刀旋转
            Projectile.rotation += spinSpeed;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.15f, 0.02f); // 逐渐减速旋转

            // 发光脉动
            glowPulse += 0.1f;

            // 粒子效果 - 暗影残留
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15), DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1, 1);
            }

            // 锁链连接粒子（稀疏）
            if (Main.rand.NextBool(5)) {
                Vector2 chainPoint = Vector2.Lerp(chainOrigin, Projectile.Center, Main.rand.NextFloat());
                var d = Dust.NewDustPerfect(chainPoint, DustID.Smoke);
                d.noGravity = true;
                d.scale = 0.6f;
                d.color = new Color(50, 50, 60);
            }

            // 加速度
            if (Projectile.velocity.Length() < 22) {
                Projectile.velocity *= 1.025f;
            }

            // 更新锁链起点（缓慢跟随）
            if (Projectile.ai[2] >= 0 && Projectile.ai[2] < Main.npc.Length) {
                NPC owner = Main.npc[(int)Projectile.ai[2]];
                if (owner.active) {
                    chainOrigin = Vector2.Lerp(chainOrigin, owner.Center, 0.1f);
                }
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(80, 60, 100).ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制锁链（从起点到镰刀）
            float waveAmp = 8f + MathF.Sin(glowPulse) * 3f;
            if (chainOrigin.To(Projectile.Center).Length() <= 2000) {
                BAWHelper.DrawGlowingChain(sb, chainOrigin, Projectile.Center,
                new Color(60, 60, 80), new Color(100, 80, 150),
                0.8f, 1.3f, waveAmp, glowPulse);
            }


            // 绘制镰刀（带拖尾）
            BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                Color.White, Projectile.scale, Projectile.oldPos, Projectile.oldRot);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyChainBound(120);

            // 命中特效
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override void OnKill(int timeLeft) {
            // 消散特效
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.5f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(8, 8);
            }
        }
    }

    /// <summary>
    /// 黑无常横扫锁链弹幕 - 环绕Boss的镰刀横扫
    /// </summary>
    public class ChainSweepProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private float sweepAngle = 0f;
        private float sweepSpeed = 0.12f;
        private float baseDistance = 280f;
        private float glowPulse = 0f;
        private NPC owner => Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.alpha = 0;
        }

        public override void AI() {
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            // 横扫运动
            sweepAngle += sweepSpeed * (Projectile.ai[1] > 0 ? 1 : -1); // ai[1]控制方向
            sweepSpeed = MathHelper.Lerp(sweepSpeed, 0.08f, 0.01f); // 逐渐减速

            // 距离脉动
            float distPulse = MathF.Sin(Projectile.timeLeft * 0.08f) * 40f;
            float currentDist = baseDistance + distPulse;

            // 计算位置
            Vector2 offset = new Vector2(MathF.Cos(sweepAngle), MathF.Sin(sweepAngle)) * currentDist;
            Projectile.Center = owner.Center + offset;

            // 镰刀始终指向外侧，带旋转
            Projectile.rotation = sweepAngle + MathHelper.PiOver2 + MathF.Sin(Projectile.timeLeft * 0.2f) * 0.3f;

            glowPulse += 0.15f;

            // 横扫轨迹粒子
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = new Vector2(-MathF.Sin(sweepAngle), MathF.Cos(sweepAngle)) * sweepSpeed * 50f;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(100, 80, 130).ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (owner == null) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 绘制连接锁链（带波动）
            float waveAmp = 12f + MathF.Sin(glowPulse * 0.5f) * 5f;
            BAWHelper.DrawGlowingChain(sb, owner.Center, Projectile.Center,
                new Color(70, 70, 90), new Color(120, 100, 180),
                0.9f, 1.4f, waveAmp, glowPulse);

            // 绘制镰刀
            BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                Color.White, Projectile.scale * 1.2f, Projectile.oldPos, Projectile.oldRot);

            // 绘制横扫轨迹光弧
            DrawSweepArc(sb);

            return false;
        }

        private void DrawSweepArc(SpriteBatch sb) {
            if (owner == null) return;

            // 绘制扫过的弧线残影
            int arcSegments = 8;
            for (int i = 1; i <= arcSegments; i++) {
                float pastAngle = sweepAngle - sweepSpeed * i * 3f * (Projectile.ai[1] > 0 ? 1 : -1);
                float pastDist = baseDistance + MathF.Sin((Projectile.timeLeft + i * 3) * 0.08f) * 40f;
                Vector2 pastPos = owner.Center + new Vector2(MathF.Cos(pastAngle), MathF.Sin(pastAngle)) * pastDist;

                float alpha = (1f - (float)i / arcSegments) * 0.3f;
                Color arcColor = new Color(100, 80, 150) * alpha;
                arcColor.A = 0;

                var tex = BAWHelper.DustTexture;
                if (tex != null) {
                    sb.Draw(tex, pastPos - Main.screenPosition, null, arcColor,
                        pastAngle, tex.Size() / 2f, 1.5f - i * 0.1f, SpriteEffects.None, 0);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyChainBound(90);
            SoundEngine.PlaySound(SoundID.NPCHit2, Projectile.Center);
        }
    }

    /// <summary>
    /// 黑无常牵引锁链弹幕 - 抓取并拉拽玩家
    /// </summary>
    public class ChainPullProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private bool hasHitPlayer = false;
        private Player targetPlayer = null;
        private float pullStrength = 0f;
        private float glowPulse = 0f;
        private float hookRotation = 0f;

        private NPC owner => Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            glowPulse += 0.1f;

            if (!hasHitPlayer) {
                // 追踪最近的玩家
                Player closest = null;
                float closestDist = 800f;
                foreach (var p in Main.player) {
                    if (p != null && p.active && !p.dead) {
                        float dist = p.Distance(Projectile.Center);
                        if (dist < closestDist) {
                            closestDist = dist;
                            closest = p;
                        }
                    }
                }

                if (closest != null) {
                    Vector2 toPlayer = (closest.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toPlayer * 18, 0.06f);
                }

                // 飞行时镰刀旋转
                hookRotation += 0.25f;
                Projectile.rotation = hookRotation;

                // 追踪粒子
                if (Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.0f;
                    d.velocity = -Projectile.velocity * 0.15f;
                }
            }
            else if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead) {
                // 牵引状态
                pullStrength = MathHelper.Lerp(pullStrength, 1.2f, 0.05f);

                Vector2 pullDirection = (owner.Center - targetPlayer.Center).SafeNormalize(Vector2.Zero);
                targetPlayer.velocity += pullDirection * pullStrength;

                // 锁链位置锁定在玩家身上
                Projectile.Center = targetPlayer.Center;

                // 镰刀嵌入动画
                hookRotation = (owner.Center - targetPlayer.Center).ToRotation() + MathHelper.Pi;
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, hookRotation, 0.1f);

                // 持续应用减速
                targetPlayer.GetModPlayer<BAWPlayer>().ApplyChainBound(5);

                // 牵引粒子（沿锁链流动）
                if (Main.rand.NextBool(2)) {
                    float t = Main.rand.NextFloat();
                    Vector2 particlePos = Vector2.Lerp(targetPlayer.Center, owner.Center, t);
                    var d = Dust.NewDustPerfect(particlePos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 0.8f;
                    d.velocity = pullDirection * 8f;
                }

                // 牵引一定时间后释放
                if (Projectile.timeLeft < 180) {
                    Projectile.Kill();
                }
            }
            else {
                Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, new Color(100, 80, 140).ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (owner == null) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 锁链颜色根据状态变化
            Color chainColor = hasHitPlayer ? new Color(120, 60, 80) : new Color(70, 70, 100);
            Color glowColor = hasHitPlayer ? new Color(200, 100, 150) : new Color(130, 110, 180);

            // 绘制锁链
            float waveAmp = hasHitPlayer ? 5f : 10f + MathF.Sin(glowPulse) * 4f;
            float tensionPulse = hasHitPlayer ? MathF.Sin(glowPulse * 2f) * 2f : 0f;

            BAWHelper.DrawGlowingChain(sb, owner.Center, Projectile.Center,
                chainColor, glowColor, 1f, 1.5f, waveAmp + tensionPulse, glowPulse);

            // 绘制镰刀
            float sickleScale = hasHitPlayer ? 1.3f : 1.1f;
            BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                Color.White, sickleScale, hasHitPlayer ? null : Projectile.oldPos, Projectile.oldRot);

            // 命中时绘制束缚效果
            if (hasHitPlayer && targetPlayer != null) {
                DrawBindingEffect(sb);
            }

            return false;
        }

        private void DrawBindingEffect(SpriteBatch sb) {
            // 在玩家周围绘制束缚环
            var tex = BAWHelper.DustTexture;
            if (tex == null) return;

            int rings = 3;
            for (int r = 0; r < rings; r++) {
                float ringRadius = 30f + r * 15f;
                float ringRotation = glowPulse * (r % 2 == 0 ? 1 : -1) * 0.5f;
                int segments = 8;

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    Vector2 pos = targetPlayer.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                    Color ringColor = new Color(150, 80, 120) * (0.4f - r * 0.1f);
                    ringColor.A = 0;

                    sb.Draw(tex, pos - Main.screenPosition, null, ringColor,
                        angle, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!hasHitPlayer) {
                hasHitPlayer = true;
                targetPlayer = target;
                Projectile.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
                target.GetModPlayer<BAWPlayer>().ApplyChainBound(180);

                // 命中冲击波
                for (int i = 0; i < 20; i++) {
                    var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = Main.rand.NextVector2CircularEdge(8, 8);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }
    }

    /// <summary>
    /// 灵魂锁链弹幕（黑白无常协同攻击）- 连接两个Boss的致命锁链
    /// </summary>
    public class SoulChainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private float swingPhase = 0f;
        private float pulsePhase = 0f;
        private float chainTension = 0f;

        private NPC blackImp => Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;
        private NPC whiteImp => Projectile.ai[1] >= 0 && (int)Projectile.ai[1] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[1]] : null;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            if (blackImp == null || whiteImp == null || !blackImp.active || !whiteImp.active) {
                Projectile.Kill();
                return;
            }

            swingPhase += 0.08f;
            pulsePhase += 0.15f;
            chainTension = MathHelper.Lerp(chainTension, 1f, 0.02f);

            // 锁链中心在两个Boss之间摆动
            Vector2 midPoint = (blackImp.Center + whiteImp.Center) / 2f;
            Vector2 perpendicular = (whiteImp.Center - blackImp.Center).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            float swingAmp = 150f * MathF.Sin(swingPhase * 0.5f);
            Projectile.Center = midPoint + perpendicular * swingAmp;

            Projectile.rotation += 0.2f;

            // 检测玩家是否在锁链区域内
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    if (IsPlayerInChainArea(p.Center)) {
                        p.GetModPlayer<BAWPlayer>().ApplySoulLock(10);

                        // 区域内持续伤害粒子
                        if (Main.rand.NextBool(5)) {
                            var d = Dust.NewDustPerfect(p.Center + Main.rand.NextVector2Circular(30, 30),
                                Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff);
                            d.noGravity = true;
                            d.scale = 1.0f;
                        }
                    }
                }
            }

            // 中心点粒子爆发
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(25, 25), dustType);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = perpendicular * MathF.Cos(swingPhase) * 3f;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(180, 150, 200).ToVector3() * 0.6f);
            Lighting.AddLight(midPoint, new Color(150, 130, 180).ToVector3() * 0.4f);
        }

        private bool IsPlayerInChainArea(Vector2 playerPos) {
            if (blackImp == null || whiteImp == null) return false;

            Vector2 lineStart = blackImp.Center;
            Vector2 lineEnd = whiteImp.Center;
            Vector2 lineDir = (lineEnd - lineStart).SafeNormalize(Vector2.Zero);
            float lineLength = Vector2.Distance(lineStart, lineEnd);

            Vector2 toPlayer = playerPos - lineStart;
            float projLength = Vector2.Dot(toPlayer, lineDir);

            if (projLength < 0 || projLength > lineLength)
                return false;

            Vector2 closestPoint = lineStart + lineDir * projLength;
            float distance = Vector2.Distance(playerPos, closestPoint);

            return distance < 120;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (blackImp == null || whiteImp == null) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 绘制黑白交织的灵魂锁链
            DrawSoulChain(sb);

            // 绘制中心的镰刀（旋转的双镰）
            DrawDualSickles(sb);

            // 绘制危险区域指示
            DrawDangerZone(sb);

            return false;
        }

        private void DrawSoulChain(SpriteBatch sb) {
            // 从黑无常到中心
            BAWHelper.DrawGlowingChain(sb, blackImp.Center, Projectile.Center,
                new Color(50, 50, 70), new Color(100, 80, 140),
                1f, 1.6f, 15f * chainTension, pulsePhase);

            // 从白无常到中心
            BAWHelper.DrawGlowingChain(sb, whiteImp.Center, Projectile.Center,
                new Color(180, 180, 200), new Color(200, 200, 255),
                1f, 1.6f, 15f * chainTension, pulsePhase + MathHelper.Pi);

            // 中心能量汇聚
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                Color.Lerp(new Color(80, 60, 100), new Color(200, 200, 220), MathF.Sin(pulsePhase) * 0.5f + 0.5f),
                new Color(150, 130, 200), 2f, pulsePhase);
        }

        private void DrawDualSickles(SpriteBatch sb) {
            var sickleTex = BAWHelper.SickleTexture;
            if (sickleTex == null) return;

            Vector2 origin = sickleTex.Size() / 2f;

            // 双镰绕中心旋转
            for (int i = 0; i < 2; i++) {
                float angle = Projectile.rotation + MathHelper.Pi * i;
                float dist = 35f + MathF.Sin(pulsePhase * 2f + i * MathHelper.Pi) * 10f;
                Vector2 sicklePos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                // 颜色交替（黑/白）
                Color sickleColor = i == 0 ? new Color(100, 100, 120) : new Color(220, 220, 240);
                Color glowColor = i == 0 ? new Color(80, 60, 120) : new Color(180, 180, 220);
                glowColor.A = 0;

                // 发光
                sb.Draw(sickleTex, sicklePos - Main.screenPosition, null, glowColor * 0.4f,
                    angle + MathHelper.PiOver2, origin, 1.4f, SpriteEffects.None, 0);

                // 主体
                sb.Draw(sickleTex, sicklePos - Main.screenPosition, null, sickleColor,
                    angle + MathHelper.PiOver2, origin, 1.1f, SpriteEffects.None, 0);
            }
        }

        private void DrawDangerZone(SpriteBatch sb) {
            if (blackImp == null || whiteImp == null) return;

            // 绘制危险区域边界
            Vector2 lineDir = (whiteImp.Center - blackImp.Center).SafeNormalize(Vector2.Zero);
            Vector2 perpendicular = lineDir.RotatedBy(MathHelper.PiOver2);
            float lineLength = Vector2.Distance(blackImp.Center, whiteImp.Center);

            var tex = BAWHelper.DustTexture;
            if (tex == null) return;

            // 上下边界线
            int segments = (int)(lineLength / 30);
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i <= segments; i++) {
                    float t = i / (float)segments;
                    Vector2 basePos = Vector2.Lerp(blackImp.Center, whiteImp.Center, t);
                    Vector2 pos = basePos + perpendicular * (120 * side);

                    float pulse = MathF.Sin(pulsePhase + t * MathHelper.TwoPi) * 0.3f + 0.7f;
                    Color borderColor = Color.Lerp(new Color(100, 80, 140), new Color(200, 180, 220), t) * pulse * 0.3f;
                    borderColor.A = 0;

                    sb.Draw(tex, pos - Main.screenPosition, null, borderColor,
                        0f, tex.Size() / 2f, 0.8f, SpriteEffects.None, 0);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplySoulLock(180);

            // 灵魂冲击
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 25; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff;
                var d = Dust.NewDustPerfect(target.Center, dustType);
                d.noGravity = true;
                d.scale = 1.8f;
                d.velocity = Main.rand.NextVector2Circular(10, 10);
            }
        }

        public override void OnKill(int timeLeft) {
            // 消散爆发
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f }, Projectile.Center);
            for (int i = 0; i < 30; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2CircularEdge(12, 12);
            }
        }
    }
}

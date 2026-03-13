using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 白无常基础幽魂弹幕 - 飘忽的追踪幽魂
    /// </summary>
    public class GhostProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float homingStrength = 0.025f;
        private float pulsePhase = 0f;
        private float ghostAlpha = 0f;
        private float wobblePhase = 0f;

        private NPC owner => Projectile.ai[2] >= 0 && (int)Projectile.ai[2] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[2]] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 100;
        }

        public override void AI() {
            // 淡入效果
            ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.05f);
            pulsePhase += 0.12f;
            wobblePhase += 0.08f;

            // 幽灵般的旋转（缓慢且飘忽）
            Projectile.rotation += MathF.Sin(wobblePhase) * 0.05f + 0.02f;

            // 追踪最近玩家
            Player target = null;
            float closestDist = 700f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        target = p;
                    }
                }
            }

            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float currentSpeed = Projectile.velocity.Length();
                float targetSpeed = MathHelper.Lerp(8f, 14f, 1f - closestDist / 700f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * targetSpeed, homingStrength);
            }

            // 幽灵般的飘动（更明显的波浪运动）
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(wobblePhase * 1.5f + Projectile.whoAmI * 0.5f) * 2f;
            Projectile.position += perpendicular * drift;

            // 主体粒子效果
            if (Main.rand.NextBool(2)) {
                Vector2 dustOffset = Main.rand.NextVector2Circular(12, 12);
                var d = Dust.NewDustPerfect(Projectile.Center + dustOffset, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f * ghostAlpha;
                d.velocity = -Projectile.velocity * 0.15f + dustOffset * 0.1f;
                d.alpha = 100;
            }

            // 尾迹粒子
            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.oldPos[4] + Projectile.Size / 2, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.6f;
                d.velocity = Main.rand.NextVector2Circular(1, 1);
                d.alpha = 150;
            }

            // 光照（脉动）
            float lightPulse = 0.4f + MathF.Sin(pulsePhase) * 0.15f;
            Lighting.AddLight(Projectile.Center, new Color(180, 180, 255).ToVector3() * lightPulse * ghostAlpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制幽灵拖尾（多层渐变）
            for (int layer = 0; layer < 2; layer++) {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    float trailAlpha = progress * 0.4f * ghostAlpha;
                    float trailScale = (0.5f + progress * 0.8f) * (layer == 0 ? 1.5f : 1f);

                    Color trailColor = layer == 0
                        ? new Color(100, 150, 255) * trailAlpha * 0.3f
                        : Color.LightCyan * trailAlpha;
                    trailColor.A = 0;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                    // 轻微偏移增加飘忽感
                    float wobble = MathF.Sin(wobblePhase + i * 0.3f) * 3f;
                    drawPos.Y += wobble;

                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 主体幽魂光球
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(200, 220, 255) * ghostAlpha,
                new Color(120, 180, 255),
                1.2f, pulsePhase);

            // 额外的幽灵"眼睛"效果
            float eyeOffset = MathF.Sin(pulsePhase * 2f) * 2f;
            Color eyeColor = Color.White * ghostAlpha * 0.8f;
            eyeColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition + new Vector2(-4, eyeOffset - 3),
                null, eyeColor, 0f, origin, 0.3f, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition + new Vector2(4, eyeOffset - 3),
                null, eyeColor, 0f, origin, 0.3f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(180);

            // 命中特效
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.8f }, Projectile.Center);

            // 幽魂消散
            for (int i = 0; i < 15; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
                d.alpha = 50;
            }
        }
    }

    /// <summary>
    /// 白无常幽灵法阵弹幕 - 环绕玩家的收缩法阵
    /// </summary>
    public class SpiritCircleProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float orbitRadius = 300f;
        private float orbitSpeed = 0.03f;
        private float pulsePhase = 0f;
        private float runeRotation = 0f;
        private Player targetPlayer = null;

        private float circleAngle {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private NPC owner => Projectile.ai[1] >= 0 && (int)Projectile.ai[1] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[1]] : null;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.alpha = 50;
        }

        public override void AI() {
            // 寻找目标玩家
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead) {
                float closestDist = float.MaxValue;
                foreach (var p in Main.player) {
                    if (p != null && p.active && !p.dead) {
                        float dist = p.Distance(Projectile.Center);
                        if (dist < closestDist) {
                            closestDist = dist;
                            targetPlayer = p;
                        }
                    }
                }
            }

            if (targetPlayer != null) {
                // 环绕玩家旋转
                circleAngle += orbitSpeed;
                orbitSpeed = MathHelper.Lerp(orbitSpeed, 0.05f, 0.005f); // 加速旋转
                orbitRadius = MathHelper.Lerp(orbitRadius, 60f, 0.008f); // 收紧包围圈

                Vector2 targetPos = targetPlayer.Center + new Vector2(MathF.Cos(circleAngle), MathF.Sin(circleAngle)) * orbitRadius;
                Projectile.velocity = (targetPos - Projectile.Center) * 0.15f;
            }

            pulsePhase += 0.1f;
            runeRotation += 0.08f;

            // 符文粒子效果
            if (Main.rand.NextBool(2)) {
                float particleAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float particleRadius = 25f + MathF.Sin(pulsePhase + particleAngle * 3) * 8f;
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(particleAngle), MathF.Sin(particleAngle)) * particleRadius;

                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = new Vector2(MathF.Cos(particleAngle + MathHelper.PiOver2), MathF.Sin(particleAngle + MathHelper.PiOver2)) * 2.5f;
                d.alpha = 100;
            }

            // 连接线粒子（连接到其他法阵）
            if (Main.rand.NextBool(8) && targetPlayer != null) {
                // 找到同类型的其他弹幕
                foreach (var proj in Main.projectile) {
                    if (proj.active && proj.type == Projectile.type && proj.whoAmI != Projectile.whoAmI) {
                        float dist = Vector2.Distance(Projectile.Center, proj.Center);
                        if (dist < 400f) {
                            Vector2 midPoint = (Projectile.Center + proj.Center) / 2f;
                            var d = Dust.NewDustPerfect(midPoint + Main.rand.NextVector2Circular(20, 20), DustID.SpectreStaff);
                            d.noGravity = true;
                            d.scale = 0.5f;
                            d.alpha = 150;
                            break;
                        }
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Color(130, 150, 255).ToVector3() * (0.4f + MathF.Sin(pulsePhase) * 0.1f));
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制多层光环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = 20f + ring * 12f;
                float ringAlpha = 0.5f - ring * 0.15f;
                int segments = 12 - ring * 2;
                float ringRotation = runeRotation * (ring % 2 == 0 ? 1 : -1);

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    float pulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                    Color runeColor = new Color(150, 180, 255) * ringAlpha * pulse;
                    runeColor.A = 0;

                    float runeScale = 0.6f + pulse * 0.3f;
                    sb.Draw(tex, pos - Main.screenPosition, null, runeColor, angle + MathHelper.PiOver4, origin, runeScale, SpriteEffects.None, 0);
                }
            }

            // 中心核心
            float corePulse = 1f + MathF.Sin(pulsePhase * 1.5f) * 0.2f;
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(180, 200, 255), new Color(100, 150, 255),
                1.5f * corePulse, pulsePhase);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(120);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = vel;
            }
        }
    }

    /// <summary>
    /// 白无常幽魂波弹幕 - 扩散的幽灵波浪
    /// </summary>
    public class GhostWaveProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float waveWidth = 1f;
        private float pulsePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            pulsePhase += 0.15f;

            // 波浪展宽
            waveWidth = MathHelper.Lerp(waveWidth, 2.5f, 0.01f);

            // 波浪运动
            float waveOffset = MathF.Sin(pulsePhase * 0.8f) * 4f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * waveOffset;

            // 渐进加速
            if (Projectile.velocity.Length() < 16) {
                Projectile.velocity *= 1.012f;
            }

            // 波浪粒子效果
            for (int i = 0; i < 3; i++) {
                float offsetY = (i - 1) * 15f * waveWidth;
                Vector2 dustPos = Projectile.Center + perpendicular * offsetY;
                dustPos += Main.rand.NextVector2Circular(8, 8);

                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 80;
            }

            Lighting.AddLight(Projectile.Center, new Color(160, 180, 255).ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            // 绘制波浪拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailWidth = waveWidth * progress;

                // 多层波浪效果
                for (int layer = -2; layer <= 2; layer++) {
                    float layerOffset = layer * 12f * trailWidth;
                    float layerAlpha = (1f - MathF.Abs(layer) / 3f) * progress * 0.4f;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 + perpendicular * layerOffset - Main.screenPosition;

                    // 波浪起伏
                    float waveY = MathF.Sin(pulsePhase + i * 0.2f + layer * 0.5f) * 3f;
                    drawPos.Y += waveY;

                    Color trailColor = Color.Lerp(new Color(150, 180, 255), new Color(200, 220, 255), progress) * layerAlpha;
                    trailColor.A = 0;

                    Vector2 trailScale = new Vector2(1f + MathF.Abs(layer) * 0.2f, 0.8f * progress);
                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 主体波浪
            float mainPulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            for (int w = -2; w <= 2; w++) {
                float wOffset = w * 15f * waveWidth;
                float wAlpha = 1f - MathF.Abs(w) / 3f;
                Vector2 wPos = Projectile.Center + perpendicular * wOffset;

                Color waveColor = new Color(180, 200, 255) * wAlpha * 0.7f;
                waveColor.A = 0;

                sb.Draw(tex, wPos - Main.screenPosition, null, waveColor, Projectile.rotation, origin,
                    new Vector2(1.5f, 1.2f * mainPulse), SpriteEffects.None, 0);
            }

            // 前端亮点
            Color headColor = Color.White * 0.6f;
            headColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, headColor,
                Projectile.rotation, origin, 0.8f * mainPulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(150);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f }, Projectile.Center);

            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 20; i++) {
                float offset = (i - 10) * 8f;
                var d = Dust.NewDustPerfect(Projectile.Center + perpendicular * offset, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }

    /// <summary>
    /// 白无常灵魂吸取弹幕 - 连接并吸取玩家生命
    /// </summary>
    public class SoulDrainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private bool isConnected = false;
        private Player connectedPlayer = null;
        private float drainTimer = 0f;
        private float connectionAlpha = 0f;
        private float pulsePhase = 0f;

        private NPC owner => Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 35;
            Projectile.height = 35;
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

            pulsePhase += 0.1f;

            if (!isConnected) {
                // 追踪玩家
                Player target = null;
                float closestDist = 500f;
                foreach (var p in Main.player) {
                    if (p != null && p.active && !p.dead) {
                        float dist = p.Distance(Projectile.Center);
                        if (dist < closestDist) {
                            closestDist = dist;
                            target = p;
                        }
                    }
                }

                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14, 0.07f);

                    // 检测命中
                    if (Projectile.Hitbox.Intersects(target.Hitbox)) {
                        isConnected = true;
                        connectedPlayer = target;
                        Projectile.velocity = Vector2.Zero;
                        SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.2f, Volume = 1.2f }, Projectile.Center);
                    }
                }

                Projectile.rotation = Projectile.velocity.ToRotation();
                connectionAlpha = MathHelper.Lerp(connectionAlpha, 0f, 0.1f);

                // 追踪粒子
                if (Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15), DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.0f;
                    d.velocity = -Projectile.velocity * 0.2f;
                }
            }
            else if (connectedPlayer != null && connectedPlayer.active && !connectedPlayer.dead) {
                // 吸取状态
                connectionAlpha = MathHelper.Lerp(connectionAlpha, 1f, 0.05f);
                Projectile.Center = connectedPlayer.Center;
                drainTimer++;

                // 从玩家吸取能量到boss
                if (drainTimer % 12 == 0) {
                    // 伤害玩家
                    connectedPlayer.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(owner.whoAmI), 8, 0);

                    // 治疗boss
                    if (owner.life < owner.lifeMax) {
                        int healAmount = 60;
                        owner.life += healAmount;
                        if (owner.life > owner.lifeMax)
                            owner.life = owner.lifeMax;
                        owner.HealEffect(healAmount);
                    }

                    // 应用debuff
                    connectedPlayer.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(30);

                    // 吸取脉冲音效
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                }

                // 吸取一定时间后断开
                if (drainTimer > 150 || !connectedPlayer.active || connectedPlayer.dead) {
                    Projectile.Kill();
                }
            }
            else {
                Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, new Color(180, 100, 220).ToVector3() * (0.4f + connectionAlpha * 0.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (owner == null) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 绘制能量吸取射线
            if (connectionAlpha > 0.1f) {
                DrawDrainBeam(sb);
            }

            // 绘制弹幕本体
            if (!isConnected) {
                // 追踪状态：绘制追踪光球
                DrawTrackingOrb(sb);
            }
            else {
                // 吸取状态：绘制附着效果
                DrawDrainEffect(sb);
            }

            return false;
        }

        private void DrawTrackingOrb(SpriteBatch sb) {
            var tex = BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(180, 100, 220) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, 0f, origin, progress * 1.5f, SpriteEffects.None, 0);
            }

            // 主体
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(200, 120, 255), new Color(150, 80, 200),
                1.8f, pulsePhase);
        }

        private void DrawDrainBeam(SpriteBatch sb) {
            // 使用高级能量射线绘制
            Color beamColor = Color.Lerp(new Color(180, 100, 220), new Color(255, 150, 255), MathF.Sin(pulsePhase) * 0.5f + 0.5f);
            beamColor *= connectionAlpha;

            BAWHelper.DrawEnergyBeam(sb, connectedPlayer?.Center ?? Projectile.Center, owner.Center, beamColor,
                12f * connectionAlpha, pulsePhase * 60f);

            // 沿射线流动的能量粒子（视觉上的，不是实际粒子）
            var tex = BAWHelper.DustTexture;
            if (tex == null || connectedPlayer == null) return;

            Vector2 start = connectedPlayer.Center;
            Vector2 end = owner.Center;
            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);

            int orbCount = 5;
            for (int i = 0; i < orbCount; i++) {
                float t = ((drainTimer * 0.03f + i / (float)orbCount) % 1f);
                Vector2 orbPos = Vector2.Lerp(start, end, t);

                float orbPulse = MathF.Sin(pulsePhase + i * MathHelper.Pi / orbCount) * 0.3f + 0.7f;
                Color orbColor = new Color(220, 150, 255) * connectionAlpha * orbPulse;
                orbColor.A = 0;

                sb.Draw(tex, orbPos - Main.screenPosition, null, orbColor, 0f, tex.Size() / 2f, 0.8f * orbPulse, SpriteEffects.None, 0);
            }
        }

        private void DrawDrainEffect(SpriteBatch sb) {
            if (connectedPlayer == null) return;

            var tex = BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 在玩家身上绘制吸取漩涡
            int spiralArms = 3;
            for (int arm = 0; arm < spiralArms; arm++) {
                float baseAngle = pulsePhase + MathHelper.TwoPi * arm / spiralArms;

                for (int i = 0; i < 8; i++) {
                    float t = i / 8f;
                    float spiralRadius = 40f * (1f - t);
                    float spiralAngle = baseAngle + t * MathHelper.TwoPi * 2f;

                    Vector2 pos = connectedPlayer.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralRadius;

                    float alpha = (1f - t) * connectionAlpha * 0.6f;
                    Color spiralColor = new Color(200, 120, 255) * alpha;
                    spiralColor.A = 0;

                    sb.Draw(tex, pos - Main.screenPosition, null, spiralColor, spiralAngle, origin, 0.5f * (1f - t * 0.5f), SpriteEffects.None, 0);
                }
            }

            // 中心吸取点
            float centerPulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.3f;
            BAWHelper.DrawGhostOrb(sb, connectedPlayer.Center,
                new Color(255, 150, 255) * connectionAlpha,
                new Color(200, 100, 220),
                1.2f * centerPulse, pulsePhase * 2f);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f }, Projectile.Center);

            // 断开连接的爆发效果
            for (int i = 0; i < 20; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(8, 8);
            }
        }
    }
}

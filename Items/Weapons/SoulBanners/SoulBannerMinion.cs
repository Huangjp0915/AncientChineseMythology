using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡悬浮体 —— 右键召唤，漂浮在玩家头顶，
    /// 以三阶段吸魂仪式攻击：
    /// 1. 蓄力旋转：幡旗高速旋转，聚集阴气
    /// 2. 展幡吸魂：停止旋转展开，向四方释放吸魂符阵
    /// 3. 收纳冷却：灵魂被吞噬，幡旗恢复静默
    /// 有敌人靠近时自动循环此过程。
    /// </summary>
    public class SoulBannerMinion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        // ── 参数 ──
        private const float IdleYOffset = 80f;
        private const float TeleportThreshold = 1600f;
        private const float DetectRadius = 550f;
        private const float AbsorbRadius = 380f;
        private const int AttackCooldown = 100;

        // 吸魂仪式阶段时长
        private const int ChargeUpFrames = 30;     // 蓄力旋转
        private const int AbsorbFrames = 50;       // 展幡吸魂
        private const int DigestFrames = 20;        // 收纳消化

        private enum RitualPhase { Idle, ChargeUp, Absorb, Digest }

        // ai[0] = 攻击冷却计时
        // ai[1] = 仪式阶段计时
        private ref float CooldownTimer => ref Projectile.ai[0];
        private ref float RitualTimer => ref Projectile.ai[1];

        // localAI
        private RitualPhase CurrentPhase {
            get => (RitualPhase)(int)Projectile.localAI[0];
            set { Projectile.localAI[0] = (int)value; RitualTimer = 0; }
        }

        private ref float SoulsAbsorbed => ref Projectile.localAI[1]; // 本轮吸魂计数（视觉用）

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.minionSlots = 0f;
            Projectile.aiStyle = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanCutTiles() => false;
        public override bool MinionContactDamage() => false;

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            // ── 存活检查 ──
            if (player.dead || !player.active) {
                player.ClearBuff(ModContent.BuffType<SoulBannerMinionBuff>());
                Projectile.Kill();
                return;
            }

            if (player.HasBuff(ModContent.BuffType<SoulBannerMinionBuff>()))
                Projectile.timeLeft = 2;

            // ── 悬浮运动 ──
            float gameTime = Main.GameUpdateCount * 0.025f;
            float bobY = MathF.Sin(gameTime * 2.5f) * 6f;
            float swayX = MathF.Sin(gameTime * 1.7f) * 10f;
            Vector2 idlePos = player.Center + new Vector2(swayX, -IdleYOffset + bobY);

            Vector2 toIdle = idlePos - Projectile.Center;
            float dist = toIdle.Length();

            if (dist > TeleportThreshold) {
                Projectile.position = idlePos;
                Projectile.velocity *= 0.1f;
                Projectile.netUpdate = true;
            }
            else if (dist > 2f) {
                float moveSpeed = CurrentPhase == RitualPhase.Idle ? 0.08f : 0.14f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toIdle * moveSpeed, 0.15f);
            }
            else {
                Projectile.velocity *= 0.92f;
            }

            // ── 仪式状态机 ──
            switch (CurrentPhase) {
                case RitualPhase.Idle:
                    IdlePhase();
                    break;
                case RitualPhase.ChargeUp:
                    ChargeUpPhase();
                    break;
                case RitualPhase.Absorb:
                    AbsorbPhase();
                    break;
                case RitualPhase.Digest:
                    DigestPhase();
                    break;
            }

            // ── 旋转 ──
            UpdateRotation();

            // ── 光照 ──
            float lightIntensity = CurrentPhase == RitualPhase.Absorb ? 1.5f : 0.6f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.1f, 0.55f) * lightIntensity);
        }

        // ── 空闲阶段：等候敌人出现 ──
        private void IdlePhase() {
            CooldownTimer++;

            // 被动灵魂粒子：灵魂向上飘散
            if (Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position - new Vector2(10f), Projectile.width + 20, Projectile.height + 20,
                    DustID.DungeonSpirit, Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.5f),
                    160, default, 0.5f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.1f));
                dust.noGravity = true;
                dust.velocity *= 0.3f;
                dust.fadeIn = 1.0f;
            }

            // 暗影火焰微光（幽鬼火感）
            if (Main.rand.NextBool(8)) {
                Vector2 flamePos = Projectile.Center + Main.rand.NextVector2Circular(12f, 16f);
                Dust flame = Dust.NewDustDirect(flamePos, 1, 1, DustID.Shadowflame,
                    0f, -Main.rand.NextFloat(0.3f, 0.8f), 180, default, 0.4f);
                flame.noGravity = true;
            }

            if (CooldownTimer < AttackCooldown) return;

            // 检测敌人
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(npc.Center, Projectile.Center) < DetectRadius) {
                    CurrentPhase = RitualPhase.ChargeUp;
                    SoulsAbsorbed = 0;
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                    break;
                }
            }
        }

        // ── 蓄力阶段：幡旗高速旋转聚阴气 ──
        private void ChargeUpPhase() {
            RitualTimer++;
            float progress = RitualTimer / ChargeUpFrames;

            // 聚气粒子：从外围向中心旋转聚集（增强版）
            int particleCount = (int)(4 + 7 * progress);
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(70f, 160f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;

                Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                Vector2 tangent = new(-toCenter.Y, toCenter.X);
                Vector2 vel = toCenter * (3f + progress * 5f) + tangent * (3.5f - progress * 2.5f);

                int dustType = i % 3 == 0 ? DustID.Shadowflame : DustID.PurpleTorch;
                Dust dust = Dust.NewDustDirect(pos, 1, 1, dustType,
                    vel.X, vel.Y, 100, default, 0.6f + 0.5f * progress);
                dust.noGravity = true;
                if (dustType == DustID.Shadowflame)
                    dust.fadeIn = 1.2f;
            }

            // 内核聚能粒子
            if (progress > 0.4f) {
                float coreIntensity = (progress - 0.4f) / 0.6f;
                for (int j = 0; j < (int)(3 * coreIntensity + 1); j++) {
                    Vector2 corePos = Projectile.Center + Main.rand.NextVector2Circular(10f, 10f);
                    Dust core = Dust.NewDustDirect(corePos, 1, 1, DustID.PurpleTorch,
                        0f, 0f, 60, default, 0.8f + 0.5f * coreIntensity);
                    core.noGravity = true;
                    core.velocity *= 0.2f;
                }
            }

            // 宝石碎光点缀
            if (progress > 0.6f && Main.rand.NextBool(3)) {
                float sparkAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float sparkR = Main.rand.NextFloat(30f, 60f) * (1f - progress * 0.3f);
                Vector2 sparkPos = Projectile.Center + sparkAngle.ToRotationVector2() * sparkR;
                Vector2 toC = (Projectile.Center - sparkPos).SafeNormalize(Vector2.Zero);
                Dust spark = Dust.NewDustDirect(sparkPos, 1, 1, DustID.GemAmethyst,
                    toC.X * 5f, toC.Y * 5f, 0, default, 0.5f);
                spark.noGravity = true;
            }

            if (RitualTimer >= ChargeUpFrames) {
                CurrentPhase = RitualPhase.Absorb;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);

                // 蓄力完成的爆发波
                for (int b = 0; b < 12; b++) {
                    float bAngle = MathHelper.TwoPi * b / 12f;
                    Vector2 bVel = bAngle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                    Dust burst = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.DungeonSpirit,
                        bVel.X, bVel.Y, 40, default, 1.3f);
                    burst.noGravity = true;
                    burst.fadeIn = 1.6f;
                }
            }
        }

        // ── 展幡吸魂阶段：向四方展开吸魂符阵 ──
        private void AbsorbPhase() {
            RitualTimer++;
            float progress = RitualTimer / (float)AbsorbFrames;
            float expandProgress = ACMUtils.QuadOut(Math.Min(progress * 3f, 1f));
            float currentRadius = AbsorbRadius * expandProgress;

            // ── 对范围内敌人造成伤害 ──
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this)) continue;

                float npcDist = Vector2.Distance(npc.Center, Projectile.Center);
                if (npcDist > currentRadius) continue;

                // 每8帧造成一次伤害
                if ((int)RitualTimer % 8 == 0 && Main.myPlayer == Projectile.owner) {
                    Player player = Main.player[Projectile.owner];
                    int damage = Projectile.damage;
                    NPC.HitInfo hit = new() {
                        Damage = damage,
                        Knockback = 0.3f,
                        HitDirection = npc.Center.X > Projectile.Center.X ? 1 : -1,
                        Crit = Main.rand.Next(100) < player.GetTotalCritChance(DamageClass.Summon),
                        DamageType = DamageClass.Summon
                    };
                    npc.StrikeNPC(hit);
                    SoulsAbsorbed++;

                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(npc, hit);
                }

                // 灵魂被抽离效果：多层次灵魂流
                if (Main.rand.NextBool(2)) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                    Vector2 toSelf = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toSelf.Y, toSelf.X);
                    Vector2 dustVel = toSelf * Main.rand.NextFloat(7f, 14f) + tangent * Main.rand.NextFloat(-4f, 4f);

                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                        dustVel.X, dustVel.Y, 40, default, 1.5f + 0.3f * progress);
                    dust.noGravity = true;
                    dust.fadeIn = 2.0f;
                }

                // 暗影伴生粒子
                if (Main.rand.NextBool(4)) {
                    Vector2 darkPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.3f, npc.height * 0.3f);
                    Vector2 toS = (Projectile.Center - darkPos).SafeNormalize(Vector2.Zero);
                    Dust dark = Dust.NewDustDirect(darkPos, 1, 1, DustID.Shadowflame,
                        toS.X * Main.rand.NextFloat(5f, 8f), toS.Y * Main.rand.NextFloat(5f, 8f),
                        80, default, 0.8f);
                    dark.noGravity = true;
                }
            }

            // ── 符阵视觉：八方吸魂纹 ──
            DrawRitualCircle(progress, currentRadius);

            if (RitualTimer >= AbsorbFrames) {
                CurrentPhase = RitualPhase.Digest;
            }
        }

        /// <summary>
        /// 绘制八方吸魂符阵粒子
        /// </summary>
        private void DrawRitualCircle(float progress, float radius) {
            int directions = 8;
            float baseAngle = progress * MathHelper.TwoPi * 0.5f;

            // ── 八方符阵射线（强化：双层 + 更多节点） ──
            for (int d = 0; d < directions; d++) {
                float angle = baseAngle + MathHelper.TwoPi * d / directions;

                // 外层射线：向内螺旋
                int pointsPerRay = 5;
                for (int p = 0; p < pointsPerRay; p++) {
                    float rayDist = radius * (0.2f + 0.8f * p / pointsPerRay);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * rayDist;

                    Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toCenter.Y, toCenter.X);
                    Vector2 vel = toCenter * 2.5f + tangent * 0.5f;

                    int type = p % 2 == 0 ? DustID.PurpleTorch : DustID.ShadowbeamStaff;
                    float scale = 0.4f + 0.4f * (1f - p / (float)pointsPerRay);
                    Dust dust = Dust.NewDustPerfect(pos, type, vel, 80, default, scale);
                    dust.noGravity = true;
                }

                // 内层射线（偏移半角度）
                float innerAngle = angle + MathHelper.TwoPi / (directions * 2);
                for (int p = 0; p < 3; p++) {
                    float rayDist = radius * 0.5f * (0.3f + 0.7f * p / 3f);
                    Vector2 pos = Projectile.Center + innerAngle.ToRotationVector2() * rayDist;
                    Vector2 toC = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 1.5f;

                    Dust inner = Dust.NewDustPerfect(pos, DustID.Shadowflame, toC, 100, default, 0.4f);
                    inner.noGravity = true;
                }
            }

            // ── 对角连接线（符文纹路感） ──
            if ((int)(RitualTimer) % 4 == 0) {
                for (int d = 0; d < 4; d++) {
                    float a1 = baseAngle + MathHelper.TwoPi * d / directions;
                    float a2 = baseAngle + MathHelper.TwoPi * (d + 4) / directions;
                    Vector2 p1 = Projectile.Center + a1.ToRotationVector2() * radius * 0.6f;
                    Vector2 p2 = Projectile.Center + a2.ToRotationVector2() * radius * 0.6f;
                    Vector2 mid = (p1 + p2) * 0.5f;
                    Dust link = Dust.NewDustPerfect(mid, DustID.PurpleTorch, Vector2.Zero, 120, default, 0.35f);
                    link.noGravity = true;
                }
            }

            // ── 外圈粒子环（双环） ──
            for (int r = 0; r < 2; r++) {
                if (!Main.rand.NextBool(2)) continue;
                float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float ringR = radius * (r == 0 ? 1f : 0.55f);
                Vector2 ringPos = Projectile.Center + ringAngle.ToRotationVector2() * ringR;
                Vector2 tangent = new Vector2(-MathF.Sin(ringAngle), MathF.Cos(ringAngle));
                float tangentDir = r == 0 ? 1f : -1f;

                int ringType = r == 0 ? DustID.PurpleTorch : DustID.DungeonSpirit;
                Dust ring = Dust.NewDustPerfect(ringPos, ringType, tangent * 2.5f * tangentDir, 60, default, 0.5f + 0.2f * r);
                ring.noGravity = true;
            }

            // ── 内核脉动 ──
            float corePulse = 0.5f + 0.5f * MathF.Sin(RitualTimer * 0.35f);
            for (int c = 0; c < (int)(2 * corePulse + 1); c++) {
                Vector2 corePos = Projectile.Center + Main.rand.NextVector2Circular(6f, 6f);
                Dust core = Dust.NewDustDirect(corePos, 1, 1, DustID.PurpleTorch,
                    0f, 0f, 50, default, 0.8f + 0.4f * corePulse);
                core.noGravity = true;
                core.velocity *= 0.15f;
            }
        }

        // ── 消化阶段：灵魂被吞噬，产生回馈 ──
        private void DigestPhase() {
            RitualTimer++;
            float progress = RitualTimer / (float)DigestFrames;

            // 灵魂向中心内爆收缩（多层次）
            int shrinkCount = (int)(3 * (1f - progress) + 1);
            for (int s = 0; s < shrinkCount; s++) {
                if (!Main.rand.NextBool(2)) continue;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(15f, 70f) * (1f - progress);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (4f + 3f * progress);

                int dustType = s % 2 == 0 ? DustID.DungeonSpirit : DustID.Shadowflame;
                Dust dust = Dust.NewDustDirect(pos, 1, 1, dustType, vel.X, vel.Y, 80, default, 1.0f + 0.3f * progress);
                dust.noGravity = true;
                if (dustType == DustID.DungeonSpirit)
                    dust.fadeIn = 1.3f;
            }

            // 消化完成时：灵魂回馈玩家（少量治疗）
            if (RitualTimer >= DigestFrames) {
                if (SoulsAbsorbed > 0 && Main.myPlayer == Projectile.owner) {
                    int healAmount = Math.Min((int)SoulsAbsorbed, 8);
                    Main.player[Projectile.owner].Heal(healAmount);
                }

                // 收束爆发（强化版）
                for (int i = 0; i < 14; i++) {
                    float bAngle = MathHelper.TwoPi * i / 14f;
                    Vector2 bVel = bAngle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                    int bType = i % 3 == 0 ? DustID.DungeonSpirit : (i % 3 == 1 ? DustID.Shadowflame : DustID.PurpleTorch);
                    Dust burst = Dust.NewDustDirect(Projectile.Center, 1, 1, bType,
                        bVel.X, bVel.Y, 60, default, 0.9f + Main.rand.NextFloat(0.3f));
                    burst.noGravity = true;
                }

                // 宝石碎光
                for (int g = 0; g < 5; g++) {
                    Dust gem = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.GemAmethyst,
                        Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 0, default, 0.6f);
                    gem.noGravity = true;
                }

                CooldownTimer = 0;
                CurrentPhase = RitualPhase.Idle;
            }
        }

        /// <summary>
        /// 根据当前阶段更新旋转方式
        /// </summary>
        private void UpdateRotation() {
            switch (CurrentPhase) {
                case RitualPhase.Idle:
                    // 轻微摇摆（像悬挂的幡旗）
                    Projectile.rotation = MathF.Sin(Main.GameUpdateCount * 0.04f) * 0.12f;
                    break;

                case RitualPhase.ChargeUp:
                    // 加速旋转（蓄力中）
                    float spinSpeed = ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames);
                    Projectile.rotation = RitualTimer * (0.1f + spinSpeed * 0.6f);
                    break;

                case RitualPhase.Absorb:
                    // 缓慢旋转（释放中）
                    Projectile.rotation += 0.03f;
                    break;

                case RitualPhase.Digest:
                    // 减速停转
                    float decel = 1f - ACMUtils.QuadOut(RitualTimer / (float)DigestFrames);
                    Projectile.rotation += 0.03f * decel;
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(texture.Width * 0.5f, texture.Height * 0.5f);

            // ── 光晕脉冲 ──
            float glowBase = CurrentPhase switch {
                RitualPhase.ChargeUp => 0.5f + 0.45f * ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames),
                RitualPhase.Absorb => 0.85f + 0.2f * MathF.Sin(RitualTimer * 0.3f),
                RitualPhase.Digest => 0.7f * (1f - RitualTimer / (float)DigestFrames),
                _ => 0.25f + 0.12f * MathF.Sin(Main.GameUpdateCount * 0.08f),
            };

            // ── 蓄力阶段：旋转残影 ──
            if (CurrentPhase == RitualPhase.ChargeUp) {
                float spinProgress = ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames);
                int trailCount = (int)(3 + 4 * spinProgress);
                for (int i = 1; i <= trailCount; i++) {
                    float pastRotation = Projectile.rotation - i * (0.15f + spinProgress * 0.25f);
                    float alpha = (1f - (float)i / (trailCount + 1)) * 0.3f * spinProgress;
                    Color trailColor = Color.Lerp(
                        new Color(100, 30, 180, 0),
                        new Color(50, 15, 120, 0),
                        (float)i / trailCount) * alpha;
                    float trailScale = Projectile.scale * (0.85f + 0.15f * (1f - (float)i / trailCount));

                    Main.EntitySpriteDraw(texture,
                        Projectile.Center - Main.screenPosition,
                        null, trailColor, pastRotation, origin,
                        trailScale, SpriteEffects.None, 0);
                }
            }

            // ── 吸魂阶段：多层光环 ──
            if (CurrentPhase == RitualPhase.Absorb) {
                // 外层大光环：脉冲膨胀
                float outerPulse = 1.45f + 0.12f * MathF.Sin(RitualTimer * 0.2f);
                Color outerAura = new Color(100, 30, 200, 0) * (glowBase * 0.15f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, outerAura, Projectile.rotation, origin,
                    Projectile.scale * outerPulse, SpriteEffects.None, 0);

                // 中层光环：偏蓝色快速脉冲
                float midPulse = 1.2f + 0.08f * MathF.Sin(RitualTimer * 0.4f + 0.8f);
                Color midAura = new Color(70, 50, 240, 0) * (glowBase * 0.2f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, midAura, Projectile.rotation, origin,
                    Projectile.scale * midPulse, SpriteEffects.None, 0);

                // 内层高亮
                Color innerAura = new Color(200, 80, 255, 0) * (glowBase * 0.25f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, innerAura, Projectile.rotation, origin,
                    Projectile.scale * 1.06f, SpriteEffects.None, 0);
            }
            else if (CurrentPhase == RitualPhase.ChargeUp) {
                // 蓄力光环
                float chargeProgress = ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames);
                Color auraColor = new Color(180, 80, 255, 0) * (glowBase * 0.35f * chargeProgress);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, auraColor, Projectile.rotation, origin,
                    Projectile.scale * (1.2f + 0.2f * chargeProgress), SpriteEffects.None, 0);
            }

            // ── 通用光晕层（色彩呼吸） ──
            float colorShift = MathF.Sin(Main.GameUpdateCount * 0.06f) * 0.5f + 0.5f;
            Color glowColor = Color.Lerp(
                new Color(130, 40, 210, 0),
                new Color(80, 50, 255, 0),
                colorShift) * glowBase;

            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, glowColor, Projectile.rotation, origin,
                Projectile.scale * 1.15f, SpriteEffects.None, 0);

            // ── 主纹理 ──
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, lightColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}

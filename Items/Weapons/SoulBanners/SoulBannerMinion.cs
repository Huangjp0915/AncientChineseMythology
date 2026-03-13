using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private RitualPhase CurrentPhase
        {
            get => (RitualPhase)(int)Projectile.localAI[0];
            set { Projectile.localAI[0] = (int)value; RitualTimer = 0; }
        }

        private ref float SoulsAbsorbed => ref Projectile.localAI[1]; // 本轮吸魂计数（视觉用）

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
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

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // ── 存活检查 ──
            if (player.dead || !player.active)
            {
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

            if (dist > TeleportThreshold)
            {
                Projectile.position = idlePos;
                Projectile.velocity *= 0.1f;
                Projectile.netUpdate = true;
            }
            else if (dist > 2f)
            {
                float moveSpeed = CurrentPhase == RitualPhase.Idle ? 0.08f : 0.14f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toIdle * moveSpeed, 0.15f);
            }
            else
            {
                Projectile.velocity *= 0.92f;
            }

            // ── 仪式状态机 ──
            switch (CurrentPhase)
            {
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
        private void IdlePhase()
        {
            CooldownTimer++;

            // 被动灵魂粒子
            if (Main.rand.NextBool(6))
            {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position - new Vector2(8f), Projectile.width + 16, Projectile.height + 16,
                    DustID.DungeonSpirit, 0f, -0.5f, 180, default, 0.5f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }

            if (CooldownTimer < AttackCooldown) return;

            // 检测敌人
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(npc.Center, Projectile.Center) < DetectRadius)
                {
                    CurrentPhase = RitualPhase.ChargeUp;
                    SoulsAbsorbed = 0;
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                    break;
                }
            }
        }

        // ── 蓄力阶段：幡旗高速旋转聚阴气 ──
        private void ChargeUpPhase()
        {
            RitualTimer++;
            float progress = RitualTimer / ChargeUpFrames;

            // 聚气粒子：从外围向中心旋转聚集
            int particleCount = (int)(3 + 5 * progress);
            for (int i = 0; i < particleCount; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(80f, 150f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;

                Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                Vector2 tangent = new(-toCenter.Y, toCenter.X);
                Vector2 vel = toCenter * (2f + progress * 4f) + tangent * (3f - progress * 2f);

                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.PurpleTorch,
                    vel.X, vel.Y, 120, default, 0.6f + 0.4f * progress);
                dust.noGravity = true;
            }

            if (RitualTimer >= ChargeUpFrames)
            {
                CurrentPhase = RitualPhase.Absorb;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        // ── 展幡吸魂阶段：向四方展开吸魂符阵 ──
        private void AbsorbPhase()
        {
            RitualTimer++;
            float progress = RitualTimer / (float)AbsorbFrames;
            float expandProgress = ACMUtils.QuadOut(Math.Min(progress * 3f, 1f));
            float currentRadius = AbsorbRadius * expandProgress;

            // ── 对范围内敌人造成伤害 ──
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this)) continue;

                float npcDist = Vector2.Distance(npc.Center, Projectile.Center);
                if (npcDist > currentRadius) continue;

                // 每8帧造成一次伤害
                if ((int)RitualTimer % 8 == 0 && Main.myPlayer == Projectile.owner)
                {
                    Player player = Main.player[Projectile.owner];
                    int damage = Projectile.damage;
                    NPC.HitInfo hit = new()
                    {
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

                // 灵魂被抽离效果：从敌人身上飞向幡旗
                if (Main.rand.NextBool(2))
                {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    Vector2 toSelf = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toSelf.Y, toSelf.X);
                    Vector2 dustVel = toSelf * Main.rand.NextFloat(6f, 12f) + tangent * Main.rand.NextFloat(-3f, 3f);

                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                        dustVel.X, dustVel.Y, 60, default, 1.4f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.8f;
                }
            }

            // ── 符阵视觉：八方吸魂纹 ──
            DrawRitualCircle(progress, currentRadius);

            if (RitualTimer >= AbsorbFrames)
            {
                CurrentPhase = RitualPhase.Digest;
            }
        }

        /// <summary>
        /// 绘制八方吸魂符阵粒子
        /// </summary>
        private void DrawRitualCircle(float progress, float radius)
        {
            // 八个方向的符阵射线
            int directions = 8;
            float baseAngle = progress * MathHelper.TwoPi * 0.5f; // 缓慢旋转

            for (int d = 0; d < directions; d++)
            {
                float angle = baseAngle + MathHelper.TwoPi * d / directions;

                // 每条射线上几个粒子
                int pointsPerRay = 3;
                for (int p = 0; p < pointsPerRay; p++)
                {
                    float rayDist = radius * (0.3f + 0.7f * p / pointsPerRay);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * rayDist;

                    // 向内螺旋的粒子
                    Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                    Vector2 vel = toCenter * 2f;

                    Dust dust = Dust.NewDustPerfect(pos, DustID.PurpleTorch, vel, 100, default, 0.5f + 0.3f * (1f - p / (float)pointsPerRay));
                    dust.noGravity = true;
                }
            }

            // 外圈粒子环
            if (Main.rand.NextBool(2))
            {
                float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 ringPos = Projectile.Center + ringAngle.ToRotationVector2() * radius;
                Vector2 tangent = new Vector2(-MathF.Sin(ringAngle), MathF.Cos(ringAngle));

                Dust dust = Dust.NewDustPerfect(ringPos, DustID.PurpleTorch, tangent * 2f, 80, default, 0.6f);
                dust.noGravity = true;
            }
        }

        // ── 消化阶段：灵魂被吞噬，产生回馈 ──
        private void DigestPhase()
        {
            RitualTimer++;
            float progress = RitualTimer / (float)DigestFrames;

            // 灵魂向中心收缩消散
            if (Main.rand.NextBool(2))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(20f, 60f) * (1f - progress);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 4f;

                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.DungeonSpirit, vel.X, vel.Y, 100, default, 1f);
                dust.noGravity = true;
            }

            // 消化完成时：灵魂回馈玩家（少量治疗）
            if (RitualTimer >= DigestFrames)
            {
                if (SoulsAbsorbed > 0 && Main.myPlayer == Projectile.owner)
                {
                    int healAmount = Math.Min((int)SoulsAbsorbed, 8);
                    Main.player[Projectile.owner].Heal(healAmount);
                }

                // 收束爆发
                for (int i = 0; i < 8; i++)
                {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.PurpleTorch,
                        0f, 0f, 100, default, 0.8f);
                    dust.noGravity = true;
                    dust.velocity = Main.rand.NextVector2CircularEdge(3f, 3f);
                }

                CooldownTimer = 0;
                CurrentPhase = RitualPhase.Idle;
            }
        }

        /// <summary>
        /// 根据当前阶段更新旋转方式
        /// </summary>
        private void UpdateRotation()
        {
            switch (CurrentPhase)
            {
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

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(texture.Width * 0.5f, texture.Height * 0.5f);

            // ── 光晕脉冲 ──
            float glowBase = CurrentPhase switch
            {
                RitualPhase.ChargeUp => 0.4f + 0.4f * ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames),
                RitualPhase.Absorb => 0.8f + 0.2f * MathF.Sin(RitualTimer * 0.3f),
                RitualPhase.Digest => 0.6f * (1f - RitualTimer / (float)DigestFrames),
                _ => 0.2f + 0.1f * MathF.Sin(Main.GameUpdateCount * 0.08f),
            };

            Color glowColor = new Color(130, 40, 210) * glowBase;

            // 蓄力和吸魂阶段：绘制额外光影层
            if (CurrentPhase is RitualPhase.ChargeUp or RitualPhase.Absorb)
            {
                Color auraColor = new Color(180, 80, 255) * (glowBase * 0.4f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, auraColor, Projectile.rotation, origin,
                    Projectile.scale * 1.35f, SpriteEffects.None, 0);
            }

            // 光晕层
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, glowColor, Projectile.rotation, origin,
                Projectile.scale * 1.15f, SpriteEffects.None, 0);

            // 主纹理
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, lightColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}

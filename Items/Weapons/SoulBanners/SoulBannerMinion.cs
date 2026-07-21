using AncientChineseMythology.Helpers;
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
    /// 万魂幡悬浮体 —— 右键召唤, 漂浮在玩家头顶。
    /// 常规循环: 蓄力旋转 → 展幡吸魂 (漩涡 shader + 吸魂光束) → 收纳消化。
    /// 大招「万魂齐哭」(右键再按下达): 聚魂 40f (收敛流线, 72% 后静默递进)
    /// → 静默收缩 8f (爆发前的塌缩) → 齐哭爆发 (亡魂军团 + 染屏 + 冲击环)。
    /// 同步: 阶段/计时/大招负载存 ai[0..2] (netUpdate 同步), 各端演出一致;
    /// 伤害与亡魂生成仅 owner 端。
    /// </summary>
    public class SoulBannerMinion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        // ── 参数 ──
        private const float IdleYOffset = 80f;
        private const float UltYOffset = 130f;
        private const float TeleportThreshold = 1600f;
        private const float DetectRadius = 550f;
        private const float AbsorbRadius = 380f;
        private const int AttackCooldown = 100;

        // 常规仪式阶段时长
        private const int ChargeUpFrames = 30;
        private const int AbsorbFrames = 50;
        private const int DigestFrames = 20;

        // 大招阶段时长
        private const int UltChargeFrames = 40;
        private const int UltSilenceFrames = 8;
        private const int UltBurstFrames = 30;

        private enum RitualPhase { Idle = 0, ChargeUp = 1, Absorb = 2, Digest = 3, UltCharge = 4, UltSilence = 5, UltBurst = 6 }

        // ai[0] = 阶段 (同步); ai[1] = 阶段计时 (同步); ai[2] = 大招消耗灵魂数 (同步, 驱动演出规模)
        private RitualPhase CurrentPhase {
            get => (RitualPhase)(int)Projectile.ai[0];
            set {
                Projectile.ai[0] = (int)value;
                RitualTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true;
            }
        }

        private ref float RitualTimer => ref Projectile.ai[1];
        private ref float UltSoulsSpent => ref Projectile.ai[2];

        // localAI: 攻击冷却 / 本轮吸魂计数 (视觉与回复用, 各端独立无碍)
        private ref float CooldownTimer => ref Projectile.localAI[0];
        private ref float SoulsAbsorbed => ref Projectile.localAI[1];

        /// <summary>大招亡魂伤害 (仅 owner 端使用; 下达指令时快照, 含成长倍率)</summary>
        private int ultDamage;

        /// <summary>是否处于大招流程 (物品端用于拒绝重复下达)</summary>
        public bool IsBusyWithUlt => CurrentPhase >= RitualPhase.UltCharge;

        // 小型灵绸 (7 节点)
        private readonly SoulBannerClothSim cloth = new(7, 9f);

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

        /// <summary>
        /// 下达大招指令 (仅 owner 客户端调用): 记录消耗魂数与亡魂伤害快照, 进入聚魂阶段。
        /// </summary>
        public void CommandUlt(int soulsSpent) {
            if (Main.myPlayer != Projectile.owner || IsBusyWithUlt)
                return;
            UltSoulsSpent = soulsSpent;
            Player owner = Main.player[Projectile.owner];
            // 亡魂伤害 = 300% 武器面板 (GetWeaponDamage 已含成长倍率)
            ultDamage = (int)(owner.GetWeaponDamage(owner.HeldItem) * 3f);
            if (ultDamage <= 0)
                ultDamage = (int)(Projectile.damage * 3f);
            CurrentPhase = RitualPhase.UltCharge;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (player.dead || !player.active) {
                player.ClearBuff(ModContent.BuffType<SoulBannerMinionBuff>());
                Projectile.Kill();
                return;
            }

            if (player.HasBuff(ModContent.BuffType<SoulBannerMinionBuff>()))
                Projectile.timeLeft = 2;

            // ── 悬浮运动 ──
            bool ulting = IsBusyWithUlt;
            float gameTime = Main.GameUpdateCount * 0.025f;
            float bobY = ulting ? 0f : MathF.Sin(gameTime * 2.5f) * 6f;
            float swayX = ulting ? 0f : MathF.Sin(gameTime * 1.7f) * 10f;
            float yOff = ulting ? UltYOffset : IdleYOffset;
            Vector2 idlePos = player.Center + new Vector2(swayX, -yOff + bobY);

            Vector2 toIdle = idlePos - Projectile.Center;
            float dist = toIdle.Length();

            if (dist > TeleportThreshold) {
                Projectile.position = idlePos;
                Projectile.velocity *= 0.1f;
                cloth.Snap(Projectile.Center);
                Projectile.netUpdate = true;
            }
            else if (dist > 2f) {
                float moveSpeed = CurrentPhase == RitualPhase.Idle ? 0.08f : (ulting ? 0.22f : 0.14f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toIdle * moveSpeed, 0.15f);
            }
            else {
                Projectile.velocity *= 0.92f;
            }

            // ── 仪式状态机 ──
            switch (CurrentPhase) {
                case RitualPhase.Idle: IdlePhase(); break;
                case RitualPhase.ChargeUp: ChargeUpPhase(); break;
                case RitualPhase.Absorb: AbsorbPhase(); break;
                case RitualPhase.Digest: DigestPhase(); break;
                case RitualPhase.UltCharge: UltChargePhase(); break;
                case RitualPhase.UltSilence: UltSilencePhase(); break;
                case RitualPhase.UltBurst: UltBurstPhase(); break;
            }

            UpdateRotation();

            // ── 灵绸 (锚在幡底, 悬挂下垂) ──
            Vector2 anchor = Projectile.Center + new Vector2(0f, 14f);
            Vector2 wind = new(MathF.Sin(gameTime * 1.3f) * 0.12f, 0f);
            if (CurrentPhase == RitualPhase.UltCharge)
                wind += Main.rand.NextVector2Circular(0.35f, 0.35f); // 聚魂乱流
            cloth.Update(anchor, wind, gameTime * 9f);

            float lightIntensity = CurrentPhase == RitualPhase.Absorb ? 1.5f
                : (IsBusyWithUlt ? 2.0f : 0.6f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.1f, 0.55f) * lightIntensity);
        }

        // ── 空闲阶段：等候敌人出现 ──
        private void IdlePhase() {
            CooldownTimer++;

            if (Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position - new Vector2(10f), Projectile.width + 20, Projectile.height + 20,
                    DustID.DungeonSpirit, Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.5f),
                    160, default, 0.5f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.1f));
                dust.noGravity = true;
                dust.velocity *= 0.3f;
                dust.fadeIn = 1.0f;
            }

            if (CooldownTimer < AttackCooldown) return;

            // 检测敌人 (owner 决策, netUpdate 广播)
            if (Main.myPlayer != Projectile.owner)
                return;
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

            // 聚气粒子：从外围向中心旋转聚集
            int particleCount = (int)(3 + 5 * progress);
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

            if (RitualTimer >= ChargeUpFrames) {
                CurrentPhase = RitualPhase.Absorb;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);

                for (int b = 0; b < 10; b++) {
                    float bAngle = MathHelper.TwoPi * b / 10f;
                    Vector2 bVel = bAngle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                    Dust burst = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.DungeonSpirit,
                        bVel.X, bVel.Y, 40, default, 1.3f);
                    burst.noGravity = true;
                    burst.fadeIn = 1.6f;
                }
            }
        }

        // ── 展幡吸魂阶段：漩涡 + 对被吸目标的光束 ──
        private void AbsorbPhase() {
            RitualTimer++;
            float progress = RitualTimer / (float)AbsorbFrames;
            float expandProgress = ACMUtils.QuadOut(Math.Min(progress * 3f, 1f));
            float currentRadius = AbsorbRadius * expandProgress;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this)) continue;

                float npcDist = Vector2.Distance(npc.Center, Projectile.Center);
                if (npcDist > currentRadius) continue;

                // 每8帧造成一次伤害 (owner 端权威 + 广播)
                if ((int)RitualTimer % 8 == 0 && Main.myPlayer == Projectile.owner) {
                    Player player = Main.player[Projectile.owner];
                    NPC.HitInfo hit = new() {
                        Damage = Projectile.damage,
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

                // 灵魂被抽离粒子 (光束承担主视觉, 粒子稀疏点缀)
                if (Main.rand.NextBool(4)) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                    Vector2 toSelf = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toSelf.Y, toSelf.X);
                    Vector2 dustVel = toSelf * Main.rand.NextFloat(7f, 14f) + tangent * Main.rand.NextFloat(-4f, 4f);

                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                        dustVel.X, dustVel.Y, 40, default, 1.5f + 0.3f * progress);
                    dust.noGravity = true;
                    dust.fadeIn = 2.0f;
                }
            }

            // 外圈引气粒子 (稀疏)
            if (Main.rand.NextBool(2)) {
                float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 ringPos = Projectile.Center + ringAngle.ToRotationVector2() * currentRadius;
                Vector2 tangent = new Vector2(-MathF.Sin(ringAngle), MathF.Cos(ringAngle));
                Dust ring = Dust.NewDustPerfect(ringPos, DustID.PurpleTorch, tangent * 2.5f, 60, default, 0.55f);
                ring.noGravity = true;
            }

            if (RitualTimer >= AbsorbFrames)
                CurrentPhase = RitualPhase.Digest;
        }

        // ── 消化阶段：灵魂被吞噬，产生回馈 ──
        private void DigestPhase() {
            RitualTimer++;
            float progress = RitualTimer / (float)DigestFrames;

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

            if (RitualTimer >= DigestFrames) {
                if (SoulsAbsorbed > 0 && Main.myPlayer == Projectile.owner) {
                    int healAmount = Math.Min((int)SoulsAbsorbed, 8);
                    Main.player[Projectile.owner].Heal(healAmount);
                }

                for (int i = 0; i < 12; i++) {
                    float bAngle = MathHelper.TwoPi * i / 12f;
                    Vector2 bVel = bAngle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                    int bType = i % 3 == 0 ? DustID.DungeonSpirit : (i % 3 == 1 ? DustID.Shadowflame : DustID.PurpleTorch);
                    Dust burst = Dust.NewDustDirect(Projectile.Center, 1, 1, bType,
                        bVel.X, bVel.Y, 60, default, 0.9f + Main.rand.NextFloat(0.3f));
                    burst.noGravity = true;
                }

                CooldownTimer = 0;
                CurrentPhase = RitualPhase.Idle;
            }
        }

        // ── 大招·聚魂：全屏灵魂流线收敛, 72% 后硬切静默 ──
        private void UltChargePhase() {
            RitualTimer++;
            float t = RitualTimer / (float)UltChargeFrames;

            // 收敛流线: 密度 ∝ sqrt(t), 72% 完成度后硬切 (尖叫前的吸气)
            if (t < 0.72f) {
                int count = (int)(2 + 6 * MathF.Sqrt(t));
                for (int i = 0; i < count; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(160f, 520f);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                    Vector2 pull = (Projectile.Center - pos) * 0.085f;
                    // 切向分量: 收敛带旋 (suction with swirl)
                    Vector2 tangent = new Vector2(-pull.Y, pull.X) * 0.45f;

                    int dustType = i % 3 == 0 ? DustID.Shadowflame : DustID.DungeonSpirit;
                    Dust dust = Dust.NewDustPerfect(pos, dustType, pull + tangent, 70, default,
                        0.7f + 0.8f * t);
                    dust.noGravity = true;
                    dust.fadeIn = 1.3f;
                }

                // 上升的隆隆震感 (取 max 不累加, t³ 增长)
                WeaponVFX.AddScreenShake(Projectile.Center, t * t * t * 2.5f);
            }

            if (RitualTimer == 2)
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.7f, Pitch = -0.7f }, Projectile.Center);
            if (RitualTimer == (int)(UltChargeFrames * 0.5f))
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);

            if (RitualTimer >= UltChargeFrames)
                CurrentPhase = RitualPhase.UltSilence;
        }

        // ── 大招·静默：万籁俱寂, 幡体塌缩 (爆发前变小) ──
        private void UltSilencePhase() {
            RitualTimer++;
            // 无粒子无声 —— 静默本身就是预告

            if (RitualTimer >= UltSilenceFrames) {
                CurrentPhase = RitualPhase.UltBurst;
                DoUltBurst();
            }
        }

        /// <summary>齐哭爆发帧：亡魂军团 + 三层音 + 震屏 (owner 生成, 各端演出)</summary>
        private void DoUltBurst() {
            Vector2 center = Projectile.Center;

            // 三层哭嚎音: 双阶梯 banshee + 低吼
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.0f, Pitch = -0.35f }, center);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.8f, Pitch = 0.25f }, center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.5f }, center);

            WeaponVFX.AddScreenShake(center, 9f);

            // 爆发粒子环
            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                int dustType = i % 3 == 0 ? DustID.Shadowflame : DustID.DungeonSpirit;
                Dust dust = Dust.NewDustDirect(center, 1, 1, dustType,
                    vel.X, vel.Y, 30, default, 1.5f + Main.rand.NextFloat(0.5f));
                dust.noGravity = true;
                dust.fadeIn = 2.0f;
            }

            // 亡魂军团 (仅 owner 生成, 扇形爆出)
            if (Main.myPlayer == Projectile.owner) {
                int wailCount = Math.Clamp(8 + (int)(UltSoulsSpent / 40f), 8, 24);
                int dmg = ultDamage > 0 ? ultDamage : (int)(Projectile.damage * 3f);
                for (int i = 0; i < wailCount; i++) {
                    float angle = MathHelper.TwoPi * i / wailCount + Main.rand.NextFloat(-0.12f, 0.12f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(19f, 25f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), center, vel,
                        ModContent.ProjectileType<SoulWailSpirit>(), dmg, 4f, Projectile.owner);
                }
            }
        }

        // ── 大招·爆发余波：计时走完回到空闲 ──
        private void UltBurstPhase() {
            RitualTimer++;

            if (RitualTimer <= 12 && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(30f, 30f);
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.DungeonSpirit,
                    Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f), 60, default, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            if (RitualTimer >= UltBurstFrames) {
                UltSoulsSpent = 0;
                CooldownTimer = AttackCooldown * 0.5f; // 大招后半冷却接常规循环
                CurrentPhase = RitualPhase.Idle;
            }
        }

        /// <summary>根据当前阶段更新旋转方式</summary>
        private void UpdateRotation() {
            switch (CurrentPhase) {
                case RitualPhase.Idle:
                    Projectile.rotation = MathF.Sin(Main.GameUpdateCount * 0.04f) * 0.12f;
                    break;

                case RitualPhase.ChargeUp: {
                    float spinSpeed = ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames);
                    Projectile.rotation = RitualTimer * (0.1f + spinSpeed * 0.6f);
                    break;
                }

                case RitualPhase.Absorb:
                    Projectile.rotation += 0.03f;
                    break;

                case RitualPhase.Digest: {
                    float decel = 1f - ACMUtils.QuadOut(RitualTimer / (float)DigestFrames);
                    Projectile.rotation += 0.03f * decel;
                    break;
                }

                case RitualPhase.UltCharge: {
                    // 聚魂急旋: t³ 加速
                    float t = RitualTimer / (float)UltChargeFrames;
                    Projectile.rotation += 0.08f + t * t * t * 0.75f;
                    break;
                }

                case RitualPhase.UltSilence:
                    // 骤停不动 —— 静默本身就是预告
                    break;

                case RitualPhase.UltBurst: {
                    float decel = 1f - Math.Clamp(RitualTimer / (float)UltBurstFrames, 0f, 1f);
                    Projectile.rotation += 0.2f * decel;
                    break;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(texture.Width * 0.5f, texture.Height * 0.5f);

            // ── 大招演出层 (全屏级, 走名额契约) ──
            float displayScale = Projectile.scale;
            if (CurrentPhase == RitualPhase.UltSilence) {
                // 塌缩: 爆发前变小
                displayScale *= MathHelper.Lerp(1f, 0.62f, RitualTimer / (float)UltSilenceFrames);
            }
            else if (CurrentPhase == RitualPhase.UltBurst) {
                float tt = 1f - Math.Clamp(RitualTimer / (float)UltBurstFrames, 0f, 1f); // 1→0
                displayScale *= MathHelper.Lerp(1f, 1.18f, tt);

                if (RitualTimer <= 5) {
                    // 起爆定格: 幽紫染屏 (强度 ≤0.13, 占全屏名额)
                    WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                        shadowTint: new Color(30, 10, 55), highlightTint: new Color(200, 150, 255),
                        intensity: 0.13f * tt, saturation: 1.05f);
                }
                else {
                    // 余波: 巨型径向泛光 (名额满自动退化柔光)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.3f, 0.85f * tt,
                        SoulBannerFX.SoulMid, 12f);
                }

                // 三重冲击环
                float ringT = Math.Clamp(RitualTimer / (float)UltBurstFrames, 0f, 1f);
                for (int r = 0; r < 3; r++) {
                    float rt = Math.Clamp(ringT * 1.4f - r * 0.14f, 0f, 1f);
                    if (rt <= 0f || rt >= 1f) continue;
                    WeaponVFX.DrawShockwaveRing(Projectile.Center,
                        20f + rt * (300f + r * 90f), 14f - r * 3f, (1f - rt) * 0.8f,
                        SoulBannerFX.SoulLit, SoulBannerFX.AbyssDeep);
                }
            }
            else if (CurrentPhase == RitualPhase.UltCharge) {
                // 聚魂涡 (大半径, 随进度展开)
                float t = RitualTimer / (float)UltChargeFrames;
                SoulBannerFX.DrawSoulVortex(Projectile.Center, 200f,
                    progress: ACMUtils.QuadOut(Math.Min(t * 1.6f, 1f)),
                    intensity: t < 0.72f ? 0.9f : 0.9f * (1f - (t - 0.72f) / 0.28f * 0.65f),
                    spin: 4.5f, seed: 0.71f);
            }

            // ── 灵绸布面 ──
            var sbOwner = Main.player[Projectile.owner].GetModPlayer<SoulBannerPlayer>();
            float growth = sbOwner.GrowthRatio;
            float ultFlash = CurrentPhase == RitualPhase.UltBurst
                ? 1f - Math.Clamp(RitualTimer / 14f, 0f, 1f) : 0f;
            SoulBannerFX.DrawSpectralCloth(cloth.Pos, 9f, growth,
                flash: ultFlash, intensity: 0.5f + 0.3f * growth, seed: 0.53f);

            // ── 吸魂阶段: 漩涡 shader + 对目标的吸魂光束 ──
            if (CurrentPhase == RitualPhase.Absorb) {
                float progress = RitualTimer / (float)AbsorbFrames;
                float expand = ACMUtils.QuadOut(Math.Min(progress * 3f, 1f));
                SoulBannerFX.DrawSoulVortex(Projectile.Center, 120f, expand,
                    intensity: 0.8f, spin: 2.6f, seed: 0.37f);

                // 吸魂光束 (同帧 ≤5 条)
                float radius = AbsorbRadius * expand;
                int beams = 0;
                for (int i = 0; i < Main.maxNPCs && beams < 5; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy(this)) continue;
                    if (Vector2.Distance(npc.Center, Projectile.Center) > radius) continue;
                    float pulse = 0.5f + 0.3f * MathF.Sin(RitualTimer * 0.4f + i);
                    ACMShaders.DrawBeam(npc.Center, Projectile.Center, 7f,
                        SoulBannerFX.SoulLit, SoulBannerFX.SoulDeep, pulse,
                        flowSpeed: 2.4f, flowScale: 2.0f);
                    beams++;
                }
            }

            // ── 光晕脉冲 ──
            float glowBase = CurrentPhase switch {
                RitualPhase.ChargeUp => 0.5f + 0.45f * ACMUtils.QuadIn(RitualTimer / (float)ChargeUpFrames),
                RitualPhase.Absorb => 0.85f + 0.2f * MathF.Sin(RitualTimer * 0.3f),
                RitualPhase.Digest => 0.7f * (1f - RitualTimer / (float)DigestFrames),
                RitualPhase.UltCharge => 0.6f + 0.55f * (RitualTimer / (float)UltChargeFrames),
                RitualPhase.UltSilence => 1.2f,
                RitualPhase.UltBurst => 1.1f * (1f - Math.Clamp(RitualTimer / (float)UltBurstFrames, 0f, 1f)) + 0.3f,
                _ => 0.25f + 0.12f * MathF.Sin(Main.GameUpdateCount * 0.08f),
            };

            // ── 蓄力/聚魂阶段：旋转残影 ──
            if (CurrentPhase == RitualPhase.ChargeUp || CurrentPhase == RitualPhase.UltCharge) {
                float total = CurrentPhase == RitualPhase.ChargeUp ? ChargeUpFrames : UltChargeFrames;
                float spinProgress = ACMUtils.QuadIn(Math.Clamp(RitualTimer / total, 0f, 1f));
                int trailCount = (int)(3 + 4 * spinProgress);
                for (int i = 1; i <= trailCount; i++) {
                    float pastRotation = Projectile.rotation - i * (0.15f + spinProgress * 0.25f);
                    float alpha = (1f - (float)i / (trailCount + 1)) * 0.3f * spinProgress;
                    Color trailColor = Color.Lerp(
                        new Color(100, 30, 180, 0),
                        new Color(50, 15, 120, 0),
                        (float)i / trailCount) * alpha;
                    float trailScale = displayScale * (0.85f + 0.15f * (1f - (float)i / trailCount));

                    Main.EntitySpriteDraw(texture,
                        Projectile.Center - Main.screenPosition,
                        null, trailColor, pastRotation, origin,
                        trailScale, SpriteEffects.None, 0);
                }
            }

            // ── 吸魂阶段：多层光环 ──
            if (CurrentPhase == RitualPhase.Absorb) {
                float outerPulse = 1.45f + 0.12f * MathF.Sin(RitualTimer * 0.2f);
                Color outerAura = new Color(100, 30, 200, 0) * (glowBase * 0.15f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, outerAura, Projectile.rotation, origin,
                    displayScale * outerPulse, SpriteEffects.None, 0);

                Color innerAura = new Color(200, 80, 255, 0) * (glowBase * 0.25f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, innerAura, Projectile.rotation, origin,
                    displayScale * 1.06f, SpriteEffects.None, 0);
            }

            // ── 通用光晕层 ──
            float colorShift = MathF.Sin(Main.GameUpdateCount * 0.06f) * 0.5f + 0.5f;
            Color glowColor = Color.Lerp(
                new Color(130, 40, 210, 0),
                new Color(80, 50, 255, 0),
                colorShift) * glowBase;

            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, glowColor, Projectile.rotation, origin,
                displayScale * 1.15f, SpriteEffects.None, 0);

            // ── 主纹理 ──
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, lightColor, Projectile.rotation, origin,
                displayScale, SpriteEffects.None, 0);

            return false;
        }
    }
}

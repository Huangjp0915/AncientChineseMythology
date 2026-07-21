using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 噩梦罗生门万咒葬神典 - 终极魔法典籍 (系列旗舰之二)
    /// 左键(无门): 在光标位置召唤罗生门 (同屏唯一), 门吸引敌人并定期伸出噩梦触手;
    /// 触手命中叠"咒层"(上限 8 层, 敌身紫红咒印可读)。
    /// 左键(门在场) = 收门清算: 门收缩闭合, 所有咒层敌人按层数爆发 —— 攒层与转移目标的决策点。
    /// </summary>
    public class NightmareRashomonMyriadCurseTome : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5660;
            Item.crit = 20;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 15;
            Item.width = 38;
            Item.height = 38;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item103;
            Item.autoReuse = false; // 召唤/收门是明确的单次决策, 不适合按住连发
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RashomonGateProj>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 决策点: 已有门 → 本次施法变为"收门清算"指令
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == player.whoAmI) {
                    if (p.ai[1] <= 0f) { // 尚未进入关门段才接受指令
                        p.ai[2] = 1f;
                        p.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.8f, Pitch = -0.5f }, p.Center);
                    }
                    return false;
                }
            }

            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CodexofMyriadDemons>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 罗生门 - 持续 5 秒的噩梦之门 (同屏唯一)。
    /// 门体 = FengduVoidRift 竖门 decal; 吸引敌人 + 每 45 帧放出 3 根噩梦触手。
    /// 收门/到期 → 12 帧收缩前静默 → 闭合帧清算全部咒层。
    /// ai[0]=Timer, ai[1]=关门段计时, ai[2]=收门信号。
    /// </summary>
    public class RashomonGateProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NightmareRashomonMyriadCurseTome";

        private ref float Timer => ref Projectile.ai[0];
        private ref float CloseTimer => ref Projectile.ai[1];
        private ref float CloseSignal => ref Projectile.ai[2];
        private const int Duration = 300;      // 5 秒
        private const int CloseFrames = 12;    // 关门收缩段 (前静默)
        private const float PullRadius = 500f;
        private const float PullStrength = 6f;
        private const float GateHalfHeight = 170f;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration + CloseFrames + 6;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.alpha = 255;
        }

        private float Opacity {
            get {
                float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
                float close = CloseTimer > 0f ? 1f - CloseTimer / CloseFrames * 0.35f : 1f;
                return fadeIn * close;
            }
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            // ---- 关门段: 收缩 + 粒子全剪 (清算前的吸气) ----
            if (CloseTimer > 0f || CloseSignal >= 1f || Timer >= Duration) {
                CloseTimer++;
                if (CloseTimer == 1f)
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 1f, Pitch = -0.7f }, Projectile.Center);
                if (CloseTimer >= CloseFrames) {
                    Reckoning();
                    Projectile.Kill();
                }
                return; // 关门段: 无吸引/无触手/无粒子 —— 前静默
            }

            float opacity = Opacity;

            // 吸引敌人
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < PullRadius && dist > 20f) {
                    float pullMult = 1f - (dist / PullRadius);
                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * PullStrength * pullMult;
                    npc.velocity += pull;
                }
            }

            // 每 45 帧放出 3 根噩梦触手 (owner 端生成)
            if (Timer % 45 == 0 && Timer < Duration - 40 && Main.myPlayer == Projectile.owner) {
                NPC target = FindNearestTarget(800f);
                if (target != null) {
                    int tendrilType = ModContent.ProjectileType<NightmareTendril>();
                    for (int i = 0; i < 3; i++) {
                        // 从门体内随机竖向位置伸出
                        Vector2 spawnPos = Projectile.Center + new Vector2(0f, Main.rand.NextFloat(-0.6f, 0.6f) * GateHalfHeight);
                        float angle = MathHelper.TwoPi / 3f * i + Main.rand.NextFloat(-0.3f, 0.3f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel,
                            tendrilType, Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
                    }
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
                }
            }

            SpawnGateParticles(opacity);
            Lighting.AddLight(Projectile.Center, 0.7f * opacity, 0.15f * opacity, 0.5f * opacity);
        }

        /// <summary>闭合清算: 所有咒层敌人按层数爆发 (伤害仅 owner 端), 全屏噩梦定调。</summary>
        private void Reckoning() {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.6f, Pitch = -0.9f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 1f, Pitch = -0.5f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 8f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.FengduVoid, 2.6f, Projectile.owner);
            FengduVFX.SpawnNightmare(Projectile.GetSource_FromThis(), Projectile.Center, 0.7f, Projectile.owner);

            int markType = ModContent.ProjectileType<RashomonCurseMark>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile mark = Main.projectile[i];
                if (!mark.active || mark.type != markType || mark.owner != Projectile.owner)
                    continue;

                int targetIdx = (int)mark.ai[0];
                if (targetIdx >= 0 && targetIdx < Main.maxNPCs && Main.npc[targetIdx].active) {
                    NPC target = Main.npc[targetIdx];
                    int stacks = Math.Max(1, (int)mark.ai[1]);

                    // 咒爆伤害仅 owner 端结算 (多人安全)
                    if (Main.myPlayer == Projectile.owner) {
                        int dmg = (int)(Projectile.damage * 1.2f * stacks);
                        target.SimpleStrikeNPC(dmg, target.position.X > Projectile.Center.X ? 1 : -1, stacks >= 6, 8f, null, false, 0, true);
                    }
                    target.AddBuff(BuffID.ShadowFlame, 600);

                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), target.Center, ACMWeaponBurst.FengduVoid, 1f + stacks * 0.15f, Projectile.owner);
                    for (int d = 0; d < Math.Min(6 + stacks * 3, 30); d++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(6f + stacks, 6f + stacks);
                        Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 60,
                            new Color(200, 40, 90), Main.rand.NextFloat(1.8f, 3f));
                        burst.noGravity = true;
                    }
                }

                mark.Kill();
            }

            // 门崩解粒子 (一次性)
            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi / 26f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle) * 0.62f, MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center + new Vector2(0, Main.rand.NextFloat(-1f, 1f) * GateHalfHeight * 0.5f),
                    DustID.Shadowflame, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                ring.noGravity = true;
            }
        }

        private NPC FindNearestTarget(float maxDist) {
            NPC closest = null;
            float bestDist = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < bestDist) { bestDist = dist; closest = npc; }
            }
            return closest;
        }

        private void SpawnGateParticles(float opacity) {
            // 门缘涡旋 (沿竖椭圆边)
            for (int i = 0; i < 4; i++) {
                float angle = Timer * 0.08f + MathHelper.TwoPi / 4f * i;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle) * 0.62f, MathF.Sin(angle)) * GateHalfHeight;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2.5f;
                vel = vel.RotatedBy(MathHelper.PiOver4);
                Dust vortex = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 80,
                    default, Main.rand.NextFloat(1.4f, 2.4f) * opacity);
                vortex.noGravity = true;
            }

            // 门内暗红噩梦火
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-80f, 80f));
                Dust fire = Dust.NewDustPerfect(pos, DustID.Torch, new Vector2(0, -Main.rand.NextFloat(1f, 2.5f)),
                    60, new Color(180, 20, 60), Main.rand.NextFloat(1.8f, 2.8f) * opacity);
                fire.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.CursedInferno, 600);
            target.AddBuff(BuffID.Slow, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = Opacity;
            float closeProgress = CloseTimer > 0f ? MathHelper.Clamp(CloseTimer / CloseFrames, 0f, 1f) : 0f;

            // 门体: FengduVoidRift 竖门 (关门段收缩 40% + 撕裂加剧 —— 变小再变响)
            float gateRadius = GateHalfHeight * (1f - 0.4f * closeProgress);
            FengduVFX.DrawVoidRift(Projectile.Center, gateRadius, opacity * (0.75f + closeProgress * 0.25f),
                0.4f + closeProgress * 0.5f, 1,
                new Color(190, 45, 90),          // 撕裂边: 罗生门暗红
                FengduVFX.VoidBright,            // 吸积辉: 系列亮紫
                seed: Projectile.whoAmI * 0.211f);

            // 门楣横梁 (暗红锋线, 门框顶部)
            Vector2 top = Projectile.Center - new Vector2(0f, gateRadius * 1.02f);
            ACMShaders.DrawBeam(top - new Vector2(gateRadius * 0.7f, 0f), top + new Vector2(gateRadius * 0.7f, 0f),
                9f, new Color(255, 90, 110), new Color(90, 15, 40), opacity * 0.8f,
                flowSpeed: 1.6f, flowScale: 2.2f, coreSharp: 2.4f);

            // 门心暗渊 (吞噬感的暗核, 关门段增亮预告)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float pulse = (1.5f + MathF.Sin(Timer * 0.15f) * 0.35f) * (1f - 0.35f * closeProgress);

                Color dark = new Color(12, 3, 18) * (opacity * 0.9f);
                dark.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark, 0f, glowOrigin,
                    new Vector2(pulse * 0.7f, pulse * 1.5f), SpriteEffects.None, 0);

                Color core = Color.Lerp(new Color(180, 20, 50), Color.White, closeProgress * 0.7f) * (opacity * (0.5f + closeProgress * 0.5f));
                core.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, core, 0f, glowOrigin,
                    new Vector2(pulse * 0.5f, pulse * 1.1f), SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 闭合冲击环 (清算帧的可见残响; 伤害已在 Reckoning 结算)
            for (int i = 0; i < 12; i++) {
                Dust fire = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 60),
                    DustID.Shadowflame, Main.rand.NextVector2Circular(6f, 6f), 60, default, Main.rand.NextFloat(2f, 3f));
                fire.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 噩梦触手 - 从罗生门放出的追踪咒触。命中叠咒层 (RashomonCurseMark, 上限 8)。
    /// </summary>
    public class NightmareTendril : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NightmareRashomonMyriadCurseTome";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 15 帧散开后追踪
            if (Timer > 15f) {
                NPC target = FindTarget(900f);
                if (target != null) {
                    Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 18f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.08f);
                }
            }

            if (Projectile.velocity.Length() > 20f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;

            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.4f, 2.4f));
                trail.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.12f, 0.35f);
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float best = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < best) { best = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.CursedInferno, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);

            // 叠咒层: 已有咒印 → +1 层并刷新; 否则挂新印 (OnHitNPC 已在 owner 端)
            int markType = ModContent.ProjectileType<RashomonCurseMark>();
            bool marked = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == markType && p.owner == Projectile.owner && (int)p.ai[0] == target.whoAmI) {
                    p.ai[1] = Math.Min(p.ai[1] + 1f, 8f);
                    p.timeLeft = RashomonCurseMark.MarkLife;
                    p.netUpdate = true;
                    marked = true;
                    break;
                }
            }
            if (!marked) {
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                    markType, 0, 0f, Projectile.owner, target.whoAmI, 1f);
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 0.9f, Projectile.owner);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.5f, Pitch = 0.2f + Main.rand.NextFloat(-0.15f, 0.15f) }, target.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 60,
                    new Color(200, 40, 90), Main.rand.NextFloat(1.4f, 2.4f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 咒触拖尾: 外暗红 + 内亮紫 (噩梦语言)
            WeaponVFX.DrawProjectileTrail(Projectile, 16f,
                new Color(170, 30, 80) * 0.9f, FengduVFX.VoidBright,
                ACMAsset.SoftGlow, uvScroll: 0.08f, subdivisions: 3);

            // 触手锋节
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 56f, Projectile.Center + dir * 16f, 12f,
                new Color(255, 80, 120), FengduVFX.VoidMid, 0.85f,
                flowSpeed: 2.4f, flowScale: 2.6f, coreSharp: 2.4f);

            float pulse = 0.6f + MathF.Sin(Timer * 0.3f) * 0.15f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, pulse, new Color(220, 50, 100) * 0.7f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 80, default, 2f);
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 咒层印记 - 挂敌纯标记 (无伤害), 门闭合时被清算消费。
    /// ai[0]=目标 NPC id, ai[1]=层数 (1~8)。层数以环绕紫红咒印可读。
    /// </summary>
    public class RashomonCurseMark : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        public const int MarkLife = 480; // 8 秒 (被触手刷新)

        private ref float TargetNPC => ref Projectile.ai[0];
        private ref float Stacks => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MarkLife;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center;

            // 层数越高咒火越浓 (≤3/帧)
            int stacks = (int)Stacks;
            if (Main.rand.NextBool(Math.Max(1, 5 - stacks / 2))) {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width * 0.6f, target.height * 0.6f);
                Dust curse = Dust.NewDustPerfect(pos, DustID.Shadowflame, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)),
                    100, new Color(200, 40, 90), 1.2f + stacks * 0.12f);
                curse.noGravity = true;
            }

            Lighting.AddLight(target.Center, 0.25f + stacks * 0.04f, 0.05f, 0.18f + stacks * 0.03f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active)
                return false;
            NPC target = Main.npc[targetIdx];
            int stacks = Math.Max(1, (int)Stacks);
            float fadeIn = MathHelper.Clamp(Timer / 12f, 0f, 1f);

            // 环绕咒印: 每层一枚紫红小印绕目标旋转 (层数可读)
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Vector2 origin = star.Size() / 2f;
                float orbitR = target.width * 0.7f + 18f;
                for (int i = 0; i < stacks; i++) {
                    float angle = Timer * 0.05f + MathHelper.TwoPi * i / stacks;
                    Vector2 pos = target.Center + angle.ToRotationVector2() * orbitR - Main.screenPosition;
                    Color sigil = Color.Lerp(new Color(220, 60, 110), FengduVFX.VoidBright, i / 8f) * (0.55f * fadeIn);
                    sigil.A = 0;
                    Main.EntitySpriteDraw(star, pos, null, sigil, -angle * 2f, origin, 0.09f + stacks * 0.004f, SpriteEffects.None, 0);
                }
            }

            // 满层警示: 8 层时目标周身暗红晕 (即将被清算的高价值目标)
            if (stacks >= 8)
                WeaponVFX.DrawGlowBurst(target.Center, 1.6f + MathF.Sin(Timer * 0.25f) * 0.25f, new Color(230, 40, 80) * 0.35f);

            return false;
        }
    }
}

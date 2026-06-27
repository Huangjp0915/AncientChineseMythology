using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 暗影寂灭终极奇点炸弹 - 终极投掷炸弹
    /// 投掷奇点核心：
    /// 阶段1（60帧）：引力场吸引敌人
    /// 阶段2：大规模内爆（600×600范围）
    /// 被击杀的敌人产生连锁奇点（更小的引力场+爆炸）
    /// </summary>
    public class ShadowOblivionSingularityBomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5000;
            Item.crit = 18;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 30;
            Item.height = 30;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<SingularityBombProj>();
            Item.shootSpeed = 14f;
            Item.consumable = false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulShatteringUnderworldBomb>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 奇点炸弹弹体 - 两阶段攻击：引力场 → 内爆
    /// </summary>
    public class SingularityBombProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/ShadowOblivionSingularityBomb";

        private ref float Timer => ref Projectile.ai[0];
        private ref float Phase => ref Projectile.ai[1];
        private const int GravityDuration = 60;
        private const float GravityRadius = 500f;
        private const float GravityStrength = 8f;
        private const float ExplosionRadius = 300f;

        public override void SetDefaults() {
            Projectile.width = 126;
            Projectile.height = 126;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 0;
        }

        public override bool? CanDamage() => Phase >= 2;

        public override void AI() {
            Timer++;

            if (Phase == 0) {
                // Flying phase - gravity affected
                Projectile.velocity.Y += 0.3f;
                Projectile.rotation += Projectile.velocity.X * 0.05f;

                // Trail particles
                if (Main.rand.NextBool(2)) {
                    Dust trail = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                        4, 4, DustID.Wraith, -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                        120, default, 1.5f);
                    trail.noGravity = true;
                }
            }
            else if (Phase == 1) {
                // Gravity well phase
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;

                float wellProgress = Timer / GravityDuration;

                // Pull enemies inward
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < GravityRadius && dist > 15f) {
                        float pullMult = (1f - dist / GravityRadius) * (0.5f + wellProgress * 0.5f);
                        Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * GravityStrength * pullMult;
                        npc.velocity += pull;
                    }
                }

                // Inward spiral particles
                int particleCount = (int)(4 + wellProgress * 12);
                for (int i = 0; i < particleCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(50f, GravityRadius * (1f - wellProgress * 0.3f));
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (3f + wellProgress * 6f);
                    vel = vel.RotatedBy(MathHelper.PiOver4 * 0.5f); // spiral
                    int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.Wraith;
                    Dust spiral = Dust.NewDustPerfect(pos, dustType, vel, 80,
                        dustType == DustID.Shadowflame ? new Color(120, 40, 200) : default,
                        Main.rand.NextFloat(1.5f, 3f));
                    spiral.noGravity = true;
                }

                // Dark center
                Dust core = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    DustID.Wraith, Vector2.Zero, 150, default, Main.rand.NextFloat(2f, 3f));
                core.noGravity = true;

                Lighting.AddLight(Projectile.Center, 0.4f * (1f - wellProgress), 0.1f, 0.6f * (1f - wellProgress));

                // Screen shake buildup
                if (Timer > GravityDuration * 0.7f && Main.rand.NextBool(3)) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.3f + wellProgress * 0.3f, Pitch = -1f + wellProgress }, Projectile.Center);
                }

                if (Timer >= GravityDuration) {
                    Detonate();
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Phase == 0) ActivateGravityWell();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase == 0) ActivateGravityWell();
        }

        private void ActivateGravityWell() {
            Phase = 1;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            SoundEngine.PlaySound(SoundID.Item104 with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);

            // Initial activation burst
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi / 30f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 60,
                    new Color(120, 40, 200), 2.5f);
                ring.noGravity = true;
            }
        }

        private void Detonate() {
            Phase = 2;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 2f, Pitch = -1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f, Pitch = -0.5f }, Projectile.Center);

            // 内爆: RadialBloom + ElementalScreenTint 虚空定调 (引力透镜→内爆的收束高潮)
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.FengduVoid, 3f, Projectile.owner);
            if (Main.myPlayer == Projectile.owner)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<SingularityImplosionFlash>(), 0, 0f, Projectile.owner);
            WeaponVFX.AddScreenShake(Projectile.Center, 11f);

            // Damage all enemies in explosion radius
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < ExplosionRadius) {
                    float distMult = 1f - (dist / ExplosionRadius) * 0.3f;
                    int dmg = (int)(Projectile.damage * 2 * distMult);
                    int dir = npc.position.X > Projectile.Center.X ? 1 : -1;
                    npc.SimpleStrikeNPC(dmg, dir, true, 20f, null, false, 0, true);
                    npc.AddBuff(BuffID.ShadowFlame, 900);
                    npc.AddBuff(BuffID.BrokenArmor, 900);

                    // Chain reaction: spawn echo at dead enemy positions
                    if (npc.life <= 0) {
                        int echoType = ModContent.ProjectileType<SingularityEcho>();
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), npc.Center, Vector2.Zero,
                            echoType, Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
                    }

                    // Knockback outward
                    Vector2 knockDir = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 16f;
                    npc.velocity += knockDir;
                }
            }

            // Massive explosion visuals
            // Ring of particles expanding outward
            for (int i = 0; i < 36; i++) {
                float angle = MathHelper.TwoPi / 36f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(12f, 25f);
                int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.Wraith;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, dustType, vel, 40,
                    dustType == DustID.Shadowflame ? new Color(160, 60, 255) : default,
                    Main.rand.NextFloat(3f, 5f));
                ring.noGravity = true;
            }

            // Secondary inner burst
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(18f, 18f);
                Dust inner = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 60,
                    new Color(200, 100, 255), Main.rand.NextFloat(2f, 4f));
                inner.noGravity = true;
            }

            // Vertical pillars
            for (int i = 0; i < 16; i++) {
                Dust upward = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                    DustID.Shadowflame, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(10f, 25f)),
                    40, new Color(160, 60, 255), Main.rand.NextFloat(2.5f, 4f));
                upward.noGravity = true;
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Phase == 0) {
                // Draw the bomb normally during flight
                return true;
            }

            if (Phase == 1) {
                float wellProgress = Timer / GravityDuration;

                // Event horizon - dark center with bright edge
                Texture2D softGlow = ACMAsset.SoftGlow;
                if (softGlow != null) {
                    Vector2 origin = softGlow.Size() / 2f;

                    // Large dark event horizon
                    float horizonSize = 1f + wellProgress * 3f;
                    Color darkColor = new Color(20, 5, 30) * 0.8f;
                    darkColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, darkColor,
                        0f, origin, horizonSize, SpriteEffects.None, 0);

                    // Bright accretion ring
                    Color ringColor = new Color(180, 80, 255) * (0.3f + wellProgress * 0.4f);
                    ringColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, ringColor,
                        0f, origin, horizonSize * 1.3f, SpriteEffects.None, 0);

                    // Outer halo
                    Color haloColor = new Color(100, 40, 180) * 0.2f;
                    haloColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, haloColor,
                        0f, origin, horizonSize * 2f, SpriteEffects.None, 0);
                }

                // Sparkle fracture lines
                Texture2D sparkle = ACMAsset.Sparkle;
                if (sparkle != null) {
                    Vector2 sparkleOrigin = sparkle.Size() / 2f;
                    float sparkleScale = 0.15f + wellProgress * 0.25f;
                    Color sparkleColor = new Color(220, 160, 255) * (0.3f + wellProgress * 0.5f);
                    sparkleColor.A = 0;
                    Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkleColor,
                        Timer * 0.08f, sparkleOrigin, sparkleScale, SpriteEffects.None, 0);
                    // Second rotated layer
                    Color sparkleColor2 = new Color(160, 80, 220) * (0.2f + wellProgress * 0.3f);
                    sparkleColor2.A = 0;
                    Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkleColor2,
                        -Timer * 0.05f + MathHelper.PiOver4, sparkleOrigin, sparkleScale * 0.8f, SpriteEffects.None, 0);
                }

                // BlankStar for singularity core
                Texture2D blankStar = ACMAsset.BlankStar;
                if (blankStar != null) {
                    Vector2 starOrigin = blankStar.Size() / 2f;
                    float pulse = 0.08f + MathF.Sin(Timer * 0.4f) * 0.02f + wellProgress * 0.05f;
                    Color starColor = new Color(255, 200, 255) * 0.6f;
                    starColor.A = 0;
                    Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor,
                        Timer * 0.15f, starOrigin, pulse, SpriteEffects.None, 0);
                }

                // 吸积冲击环 (向心收口, 读作"即将内爆"的可读预警)
                float ringR = MathHelper.Lerp(GravityRadius * 0.5f, 40f, wellProgress);
                WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 18f, (0.3f + wellProgress * 0.5f),
                    new Color(190, 120, 255), new Color(30, 8, 55));
            }

            return false;
        }

        // ★ 签名时刻: GenericWarp 黑洞引力透镜 (居中于奇点, 本武器唯一全屏后处理)
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu || Phase != 1)
                return;
            float wellProgress = MathHelper.Clamp(Timer / GravityDuration, 0f, 1f);
            float intensity = 0.4f + wellProgress * 0.6f; // 蓄力越久, 吞噬越强
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, intensity);
            fx.Parameters["uRadius"]?.SetValue(0.4f + wellProgress * 0.35f);
            fx.Parameters["uWarpScale"]?.SetValue(1.8f);
            fx.Parameters["uChroma"]?.SetValue(0.85f);
            fx.Parameters["uRadialPull"]?.SetValue(0.7f + wellProgress * 0.5f); // 强向心吸入
            fx.Parameters["uMode"]?.SetValue(4f);  // void 黑洞: 中心压暗成黑洞
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.22f, 0.08f, 0.4f, 0.6f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }
    }

    /// <summary>
    /// 奇点回响 - 连锁反应产生的次级奇点
    /// </summary>
    public class SingularityEcho : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/ShadowOblivionSingularityBomb";

        private ref float Timer => ref Projectile.ai[0];
        private const int EchoDuration = 30;
        private const float EchoRadius = 200f;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EchoDuration + 1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float progress = Timer / EchoDuration;

            // Smaller gravity pull
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < EchoRadius && dist > 10f) {
                    float pull = (1f - dist / EchoRadius) * 5f;
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * pull;
                }
            }

            // Particles
            for (int i = 0; i < 4; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(20f, EchoRadius * (1f - progress * 0.5f));
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 4f;
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 80,
                    new Color(120, 40, 200), Main.rand.NextFloat(1.5f, 2.5f) * (1f - progress));
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.3f * (1f - progress), 0.1f, 0.4f * (1f - progress));

            if (Timer >= EchoDuration) {
                // Mini detonation
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.3f }, Projectile.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.FengduVoid, 1.4f, Projectile.owner);

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    if (Vector2.Distance(Projectile.Center, npc.Center) < EchoRadius) {
                        npc.SimpleStrikeNPC(Projectile.damage, npc.position.X > Projectile.Center.X ? 1 : -1,
                            false, 12f, null, false, 0, true);
                        npc.AddBuff(BuffID.ShadowFlame, 300);
                    }
                }

                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi / 16f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 12f);
                    Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 40,
                        new Color(160, 60, 255), Main.rand.NextFloat(2f, 3f));
                    ring.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / EchoDuration;
            float opacity = 1f - progress;

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                float size = 0.5f + progress;
                Color dark = new Color(20, 5, 30) * 0.6f * opacity;
                dark.A = 120;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark,
                    0f, origin, size, SpriteEffects.None, 0);

                Color ring = new Color(160, 60, 255) * 0.4f * opacity;
                ring.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, ring,
                    0f, origin, size * 1.4f, SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 奇点内爆演出 (纯视觉, 本地客户端): ElementalScreenTint 虚空黑紫定调 + RadialBloom 内爆核。
    /// </summary>
    public class SingularityImplosionFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f);

            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(new Color(70, 24, 130).ToVector3(), 0.34f * life));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(12, 4, 26).ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.55f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.4f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.26f, life * 0.9f, new Color(180, 110, 255), 12f);
            return false;
        }
    }
}

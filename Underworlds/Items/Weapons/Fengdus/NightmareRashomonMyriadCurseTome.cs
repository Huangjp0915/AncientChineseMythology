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
    /// 噩梦罗生门万咒葬神典 - 终极魔法典籍
    /// 在光标位置召唤罗生门，持续4秒
    /// 门会吸引并持续伤害周围敌人，每秒释放噩梦触手追踪敌人
    /// 同一时间仅允许一扇门存在
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
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RashomonGateProj>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<RashomonGateProj>()] < 3;
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
    /// 罗生门 - 持续4秒的虚空之门
    /// 吸引敌人 + 持续伤害 + 定期释放噩梦触手
    /// </summary>
    public class RashomonGateProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NightmareRashomonMyriadCurseTome";

        private ref float Timer => ref Projectile.ai[0];
        private const int Duration = 240; // 4 seconds
        private const float PullRadius = 500f;
        private const float DamageRadius = 200f;
        private const float PullStrength = 6f;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float lifeProgress = Timer / Duration;
            float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Duration - Timer) / 30f, 0f, 1f);
            float opacity = fadeIn * fadeOut;

            // Pull enemies toward gate
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

            // Spawn nightmare tendrils every 60 frames
            if (Timer % 60 == 0 && Timer < Duration - 30) {
                NPC target = FindNearestTarget(800f);
                if (target != null) {
                    int tendrilType = ModContent.ProjectileType<NightmareTendril>();
                    for (int i = 0; i < 3; i++) {
                        float angle = MathHelper.TwoPi / 3f * i + Main.rand.NextFloat(-0.3f, 0.3f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                            tendrilType, Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
                    }
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                }
            }

            // Spawn gate particles
            SpawnGateParticles(opacity);

            // Rotation effect
            Projectile.rotation += 0.03f;

            // Lighting
            Lighting.AddLight(Projectile.Center, 0.8f * opacity, 0.2f * opacity, 0.4f * opacity);
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
            // Vortex particles spiraling inward
            for (int i = 0; i < 6; i++) {
                float angle = Timer * 0.08f + MathHelper.TwoPi / 6f * i;
                float radius = 80f + Main.rand.NextFloat(-10f, 30f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
                vel = vel.RotatedBy(MathHelper.PiOver4);
                Dust vortex = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 80,
                    default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                vortex.noGravity = true;
            }

            // Inner crimson fire
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(40, 40);
                Dust fire = Dust.NewDustPerfect(pos, DustID.Torch, Main.rand.NextVector2Circular(2f, 2f),
                    60, new Color(180, 20, 60), Main.rand.NextFloat(2f, 3f) * opacity);
                fire.noGravity = true;
            }

            // Outer eldritch green mist
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(60f, 120f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Dust mist = Dust.NewDustPerfect(pos, DustID.CursedTorch,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 100,
                    default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                mist.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.CursedInferno, 600);
            target.AddBuff(BuffID.Slow, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Duration - Timer) / 30f, 0f, 1f);
            float opacity = fadeIn * fadeOut;

            // 罗生门: ArenaRunic 门框符纹 (牢笼罩模式, 赤红×鬼绿的虚空之门框)
            Effect runic = ACMShaders.ArenaRunic;
            if (runic != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, 150f, out Vector2 uv, out float rFrac, out float aspect);
                runic.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                runic.Parameters["uCenter"]?.SetValue(uv);
                runic.Parameters["uRadius"]?.SetValue(rFrac);
                runic.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(opacity * 0.9f, 0f, 1f));
                runic.Parameters["uAspect"]?.SetValue(aspect);
                runic.Parameters["uColorPrimary"]?.SetValue(new Color(210, 40, 70).ToVector4());
                runic.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
                runic.Parameters["uRuneFreq"]?.SetValue(14f);
                runic.Parameters["uMode"]?.SetValue(1f);  // 牢笼罩: 门框 + 穹网 = 虚空门框
                runic.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, runic);
            }

            // 门心暗渊 + 双色晕 (吞噬感的暗核)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float pulse = 1.5f + MathF.Sin(Timer * 0.15f) * 0.4f;

                Color dark = new Color(16, 4, 14) * (opacity * 0.85f);
                dark.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark, 0f, glowOrigin, pulse, SpriteEffects.None, 0);

                Color coreColor = new Color(180, 20, 50) * opacity * 0.55f;
                coreColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, coreColor, 0f, glowOrigin, pulse * 1.4f, SpriteEffects.None, 0);

                Color haloColor = new Color(40, 120, 60) * opacity * 0.3f;
                haloColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, haloColor, 0f, glowOrigin, pulse * 2.2f, SpriteEffects.None, 0);
            }

            return false;
        }

        // ★ 签名时刻: GenericWarp 虚空吞噬扭曲 (居中于门, 本武器唯一全屏后处理, 走名额仲裁)
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu)
                return;
            float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Duration - Timer) / 30f, 0f, 1f);
            float opacity = fadeIn * fadeOut;
            float warp = opacity * 0.7f;
            if (warp < 0.05f || !ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, warp);
            fx.Parameters["uRadius"]?.SetValue(0.5f);
            fx.Parameters["uWarpScale"]?.SetValue(1.5f);
            fx.Parameters["uChroma"]?.SetValue(0.5f);
            fx.Parameters["uRadialPull"]?.SetValue(0.8f); // 虚空吸入
            fx.Parameters["uMode"]?.SetValue(4f);          // void 黑洞档
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.32f, 0.06f, 0.12f, 0.55f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = 0.3f }, Projectile.Center);
            // 罗生门坍缩: 鬼绿审判泛光 + 冲击环
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.GhostGreen, 2.2f, Projectile.owner);
            WeaponVFX.AddScreenShake(Projectile.Center, 5f);
            // Gate collapse implosion
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi / 20f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Dust fire = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                    DustID.CursedTorch, Main.rand.NextVector2Circular(6f, 6f), 60, default, Main.rand.NextFloat(2f, 3f));
                fire.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 噩梦触手 - 从罗生门释放的追踪触手
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

            // Initial scatter for 15 frames, then home
            if (Timer > 15f) {
                NPC target = FindTarget(900f);
                if (target != null) {
                    Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 18f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.08f);
                }
            }

            // Speed cap
            if (Projectile.velocity.Length() > 20f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;

            // Trail particles
            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, Main.rand.NextBool() ? DustID.Shadowflame : DustID.CursedTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.15f, 0.3f);
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

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.GhostGreen, 0.9f, Projectile.owner);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 60, default, Main.rand.NextFloat(1.5f, 2.5f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 噩梦触手: 双层 ribbon 拖尾 (外赤红 + 内鬼绿) —— 蠕动的咒触
            WeaponVFX.DrawProjectileTrail(Projectile, 16f,
                new Color(200, 30, 60) * 0.9f, new Color(120, 230, 140),
                ACMAsset.SoftGlow, uvScroll: 0.08f, subdivisions: 3);

            // BeamGrad 触手锋节 (沿冲刺方向的赤绿锐节)
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 56f, Projectile.Center + dir * 16f, 12f,
                new Color(255, 70, 110), new Color(60, 170, 90), 0.85f,
                flowSpeed: 2.4f, flowScale: 2.6f, coreSharp: 2.4f);

            // 触手首端柔光
            float pulse = 0.6f + MathF.Sin(Timer * 0.3f) * 0.15f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, pulse, new Color(220, 40, 80) * 0.7f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 80, default, 2f);
                death.noGravity = true;
            }
        }
    }
}

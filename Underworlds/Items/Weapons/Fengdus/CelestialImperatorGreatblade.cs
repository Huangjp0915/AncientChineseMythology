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
    /// 酆都万劫寂灭黑帝刀 - 终极近战大刀
    /// 三段连击系统：前两击释放暗红刀气扇形扫射，第三击引爆虚空漩涡将敌人拖入深渊
    /// 非Boss敌人25%血量以下直接斩杀，暴击时审判全屏敌人
    /// </summary>
    public class CelestialImperatorGreatblade : ModItem
    {
        private int comboCounter = 0;

        public override void SetDefaults() {
            Item.damage = 12800;
            Item.crit = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 90;
            Item.height = 90;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 16f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.scale = 1.8f;
            Item.shoot = ModContent.ProjectileType<ImperatorSlash>();
            Item.shootSpeed = 22f;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Torch, player.velocity.X * 0.5f, player.velocity.Y * 0.5f,
                    60, new Color(40, 0, 0), 2.8f);
                d.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith, 0f, -3f, 120, default, 2f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 900);
            target.AddBuff(BuffID.OnFire3, 900);
            target.AddBuff(BuffID.Ichor, 900);

            if (!target.boss && target.life < target.lifeMax * 0.25f) {
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.8f }, target.Center);
                // 处决: 致命红命中爆发 + 全屏审判 (ElementalScreenTint 暗红 + RadialBloom 核)
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center, ACMWeaponBurst.LethalRed, 2.4f, player.whoAmI);
                SpawnJudgment(player, target.Center, execute: true);
                WeaponVFX.AddScreenShake(target.Center, 7f);
            }

            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.2f, Pitch = -0.6f }, target.Center);
                // 暴击连锁审判: 全屏黑紫定调 + 核心泛光
                SpawnJudgment(player, target.Center, execute: false);
                WeaponVFX.AddScreenShake(target.Center, 5f);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 600f) {
                        nearby.SimpleStrikeNPC(damageDone, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.ShadowFlame, 600);
                    }
                }
            }
            else {
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 1.1f, player.whoAmI);
            }
        }

        // 全屏审判演出弹幕 (仅本地客户端生成, 纯视觉)
        private static void SpawnJudgment(Player player, Vector2 worldPos, bool execute) {
            if (Main.dedServ || Main.myPlayer != player.whoAmI)
                return;
            Projectile.NewProjectile(player.GetSource_Misc("ImperatorJudgment"), worldPos, Vector2.Zero,
                ModContent.ProjectileType<ImperatorJudgmentFlash>(), 0, 0f, player.whoAmI, execute ? 1f : 0f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            comboCounter = (comboCounter + 1) % 3;

            if (comboCounter < 2) {
                int dir = comboCounter == 0 ? 1 : -1;
                for (int i = -2; i <= 2; i++) {
                    Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(i * 7 * dir));
                    Vector2 spawnPos = player.Center + perturbedVel.SafeNormalize(Vector2.UnitX) * 50f;
                    Projectile.NewProjectile(source, spawnPos, perturbedVel, type, (int)(damage * 0.8f), knockback * 0.5f, player.whoAmI);
                }
            }
            else {
                for (int i = -2; i <= 2; i++) {
                    Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(i * 7));
                    Projectile.NewProjectile(source, player.Center + perturbedVel.SafeNormalize(Vector2.UnitX) * 50f, perturbedVel, type, damage, knockback, player.whoAmI);
                }
                int eruptionType = ModContent.ProjectileType<ImperatorVoidEruption>();
                Projectile.NewProjectile(source, player.Center + velocity.SafeNormalize(Vector2.UnitX) * 80f, velocity * 0.1f, eruptionType, damage * 2, knockback * 2f, player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f, Pitch = -0.6f }, player.Center);
                for (int i = 0; i < 60; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(20f, 20f);
                    Dust boom = Dust.NewDustPerfect(player.Center + velocity.SafeNormalize(Vector2.UnitX) * 80f, DustID.Torch, vel, 40, new Color(120, 0, 0), 4f);
                    boom.noGravity = true;
                }
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<YamasDeicide>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class ImperatorSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/CelestialImperatorGreatblade";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 10;
            Projectile.timeLeft = 50;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.alpha = 30;
        }

        public override void AI() {
            Projectile.alpha += 4;
            if (Projectile.alpha > 255) { Projectile.Kill(); return; }
            Projectile.rotation = Projectile.velocity.ToRotation();
            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 1.5f * brightness, 0.2f * brightness, 0.2f * brightness);

            for (int i = 0; i < 4; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(20, 20),
                    4, 4, DustID.Torch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    80, new Color(60, 0, 0), Main.rand.NextFloat(2f, 3f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust wraith = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                    4, 4, DustID.Wraith, 0f, -1.5f, 100, default, 2f);
                wraith.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.OnFire3, 600);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 1f, Projectile.owner);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 60, new Color(120, 30, 60), Main.rand.NextFloat(2.5f, 3.5f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = (255 - Projectile.alpha) / 255f;

            // 双层刀气拖尾 (外宽暗紫 + 内窄赤芯), 沿历史点构成弧形扫劈
            WeaponVFX.DrawProjectileTrail(Projectile, 46f * Projectile.scale,
                new Color(90, 25, 150) * opacity, new Color(255, 70, 90) * opacity,
                ACMAsset.GlaciateWave, uvScroll: -0.04f, subdivisions: 3);

            // BeamGrad 刀刃锋线 (尾→刃, 赤芯黑紫边), 即时挥砍的锐利前沿
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tip = Projectile.Center + dir * 60f * Projectile.scale;
            Vector2 tail = Projectile.Center - dir * 70f * Projectile.scale;
            ACMShaders.DrawBeam(tail, tip, 26f * Projectile.scale,
                new Color(255, 80, 95), new Color(70, 18, 120), opacity * 0.9f,
                flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

            // 刃心柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.4f + opacity) * Projectile.scale, new Color(160, 40, 90) * (opacity * 0.7f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    60, new Color(60, 0, 0), Main.rand.NextFloat(2f, 3f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 虚空吞噬漩涡 - 第三击终结技，将敌人拖入深渊
    /// </summary>
    public class ImperatorVoidEruption : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/CelestialImperatorGreatblade";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 45;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity *= 0.92f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < 400f && dist > 30f) {
                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 4f;
                    npc.velocity += pull;
                }
            }

            float progress = Timer / 45f;
            Lighting.AddLight(Projectile.Center, 2f * (1f - progress), 0.3f * (1f - progress), 0.3f * (1f - progress));

            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(20f, 120f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 40, new Color(120, 0, 0), Main.rand.NextFloat(2.5f, 4f));
                d.noGravity = true;
            }
            for (int i = 0; i < 4; i++) {
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(4f, 12f)).RotatedByRandom(MathHelper.TwoPi);
                Dust wraith = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(50, 50), DustID.Wraith, vel, 100, default, 3f);
                wraith.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 900);
            target.AddBuff(BuffID.OnFire3, 900);
            target.AddBuff(BuffID.Ichor, 900);
            target.AddBuff(BuffID.BrokenArmor, 900);
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 45f;
            float opacity = 1f - progress;

            // 吸卷冲击环 (向心收口的虚空裂口)
            float ringR = MathHelper.Lerp(150f, 24f, progress);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 16f, opacity * 0.8f,
                new Color(190, 120, 255), new Color(40, 10, 70));

            // 吸积旋臂 (赤金边的环面碎屑)
            Texture2D emberShards = ACMAsset.EmberShards;
            if (emberShards != null) {
                Vector2 origin = emberShards.Size() / 2f;
                Color shardColor = new Color(200, 90, 255) * opacity * 0.6f;
                shardColor.A = 0;
                float shardScale = 0.6f + progress * 0.4f;
                Main.EntitySpriteDraw(emberShards, Projectile.Center - Main.screenPosition, null, shardColor, Timer * 0.18f, origin, shardScale, SpriteEffects.None, 0);
            }

            // 奇点暗芯 + 紫晕
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                Color dark = new Color(12, 4, 22) * (0.9f * opacity);
                dark.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark, 0f, origin, (2.6f + MathF.Sin(Timer * 0.4f) * 0.4f), SpriteEffects.None, 0);
                Color halo = new Color(120, 50, 210) * (opacity * 0.5f);
                halo.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, halo, 0f, origin, 3.6f, SpriteEffects.None, 0);
            }
            return false;
        }

        // 第三击虚空漩涡的签名时刻: GenericWarp 黑洞吸卷扭曲 (本武器唯一全屏后处理, 走名额仲裁)
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu)
                return;
            float progress = Timer / 45f;
            float warp = MathHelper.Clamp(1f - progress, 0f, 1f);
            if (warp < 0.05f || !ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, warp);
            fx.Parameters["uRadius"]?.SetValue(0.55f);
            fx.Parameters["uWarpScale"]?.SetValue(1.5f);
            fx.Parameters["uChroma"]?.SetValue(0.7f);
            fx.Parameters["uRadialPull"]?.SetValue(0.9f); // 向心吸入
            fx.Parameters["uMode"]?.SetValue(4f);          // void 黑洞档
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.32f, 0.12f, 0.5f, 0.7f));
            SpriteBatch sb = Main.spriteBatch;
            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: true);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.8f }, Projectile.Center);
            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(18f, 18f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 40, new Color(100, 0, 0), Main.rand.NextFloat(3f, 5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 30; i++) {
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(6f, 16f)).RotatedByRandom(MathHelper.TwoPi);
                Dust death = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(2.5f, 4f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 全屏审判演出 (纯视觉, 本地客户端): 处决=暗红定调; 暴击=黑紫定调。
    /// ElementalScreenTint 短暂染屏 + RadialBloom 核心泛光 (单次"短暂定调")。
    /// </summary>
    public class ImperatorJudgmentFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 26;

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

            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f); // 1→0
            bool execute = Projectile.ai[0] > 0.5f;
            Color tint = execute ? new Color(140, 16, 22) : new Color(70, 24, 130);
            Color tintLow = execute ? new Color(30, 2, 6) : new Color(14, 4, 30);
            Color bloom = execute ? new Color(255, 60, 70) : new Color(180, 110, 255);

            // ElementalScreenTint 短暂染屏 (不读 screenTarget, 不占全屏名额)
            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.34f * life));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(tintLow.ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.5f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.4f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 核心泛光 (RadialBloom 占全屏名额, 名额被占自动退化柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.2f, life * 0.85f, bloom, 10f);
            return false;
        }
    }
}

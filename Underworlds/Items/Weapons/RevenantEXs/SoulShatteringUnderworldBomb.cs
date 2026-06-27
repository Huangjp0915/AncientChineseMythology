using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 冥岩碎魂黄泉雷 - NetherRockSoulbomb的终极升级版
    /// 能粉碎灵魂、开启黄泉之门的冥岩重雷
    /// 特殊机制：投掷后分裂为3枚子雷，每枚爆炸范围巨大，击杀后灵魂碎片扩散再爆炸
    /// </summary>
    public class SoulShatteringUnderworldBomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1700;
            Item.crit = 14;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 36;
            Item.height = 36;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 16f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<SoulShatteringBombProj>();
            Item.shootSpeed = 12f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 投掷主雷
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NetherRockSoulbomb>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class SoulShatteringBombProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulShatteringUnderworldBomb";
        private ref float Timer => ref Projectile.ai[0];
        private ref float HasBounced => ref Projectile.ai[1];
        private const int FuseTime = 60;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FuseTime + 30;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) Projectile.velocity.Y = 16f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            float fuseProgress = Timer / FuseTime;
            float flicker = MathF.Sin(Timer * (0.4f + fuseProgress * 0.6f)) * 0.5f + 0.5f;
            Lighting.AddLight(Projectile.Center, 1f * flicker * fuseProgress, 0.4f * flicker * fuseProgress, 1.2f * flicker * fuseProgress);

            // 引信粒子
            for (int i = 0; i < 2; i++) {
                Dust fuse = Dust.NewDustDirect(
                    Projectile.Center + new Vector2(0, -Projectile.height * 0.4f), 4, 4, DustID.PurpleTorch,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f),
                    80, default, Main.rand.NextFloat(1.2f, 2f)
                );
                fuse.noGravity = true;
            }
            if (fuseProgress > 0.4f) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.Center, 8, 8, DustID.Smoke,
                    0f, -1.5f, 180, new Color(100, 40, 160), Main.rand.NextFloat(1.2f, 2f)
                );
                smoke.noGravity = true;
            }
            // 临近爆炸时震动效果
            if (fuseProgress > 0.7f && Main.rand.NextBool(2)) {
                Dust warn = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10), 4, 4, DustID.Shadowflame,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    100, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                warn.noGravity = true;
            }

            if (Timer >= FuseTime) { Explode(); }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { Explode(); }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (HasBounced == 0) {
                HasBounced = 1;
                if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
                if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
                Projectile.velocity *= 0.5f;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.1f }, Projectile.Center);
                return false;
            }
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        private void Explode() {
            if (Projectile.timeLeft <= 0) return;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.position -= new Vector2(160, 160);
            Projectile.width = 320;
            Projectile.height = 320;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.5f }, Projectile.Center);
            Vector2 explosionCenter = Projectile.Center;

            // 巨大爆炸粒子效果
            for (int i = 0; i < 60; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(16f, 16f);
                Dust fire = Dust.NewDustPerfect(explosionCenter, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(2.5f, 4.5f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(14f, 14f);
                vel.Y -= 3f;
                Dust soul = Dust.NewDustPerfect(explosionCenter, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(2f, 3.5f));
                soul.noGravity = true;
            }
            for (int i = 0; i < 30; i++) {
                Vector2 smokeVel = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-10f, -3f));
                Dust smoke = Dust.NewDustPerfect(explosionCenter, DustID.Smoke, smokeVel, 180, new Color(100, 40, 160), Main.rand.NextFloat(3f, 5f));
                smoke.noGravity = true;
            }
            // 碎魂冲击波
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi / 30f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust ring = Dust.NewDustPerfect(explosionCenter, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(2.5f, 4f));
                ring.noGravity = true;
            }

            Lighting.AddLight(explosionCenter, 3f, 1.5f, 4f);

            // 升级演出: 一段大爆 GenericWarp 虚空冲击扭曲 + 层叠冲击环 (仅本机)
            SoulShatterBlastFX.Spawn(Projectile.GetSource_FromThis(), explosionCenter, 0, Projectile.owner);
            WeaponVFX.AddScreenShake(explosionCenter, 6f);

            // 范围debuff
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(explosionCenter, npc.Center) < 240f) {
                    npc.AddBuff(BuffID.ShadowFlame, 600);
                    npc.AddBuff(BuffID.OnFire3, 600);
                    npc.AddBuff(BuffID.Ichor, 600);
                }
            }

            // 分裂为3枚子雷
            for (int i = 0; i < 3; i++) {
                float angle = MathHelper.TwoPi / 3f * i + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 subVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 10f);
                subVel.Y -= 4f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), explosionCenter, subVel,
                    ModContent.ProjectileType<SoulShatteringSubBomb>(),
                    Projectile.damage / 2, Projectile.knockBack * 0.5f, Projectile.owner
                );
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float fuseProgress = Timer / FuseTime;

            Color mainColor = Color.Lerp(lightColor, new Color(220, 160, 255), fuseProgress * 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null && fuseProgress > 0.2f) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float glowIntensity = (fuseProgress - 0.2f) / 0.8f;
                float pulse = 0.6f + MathF.Sin(Timer * (0.4f + fuseProgress * 0.5f)) * 0.2f;
                Color glowColor = new Color(220, 100, 255) * glowIntensity * 0.7f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glowColor, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null && fuseProgress > 0.4f) {
                Vector2 sparkleOrigin = sparkle.Size() / 2f;
                float sparkIntensity = (fuseProgress - 0.4f) / 0.6f;
                Color sparkColor = new Color(240, 120, 255) * sparkIntensity * 0.5f;
                sparkColor.A = 0;
                float sparkScale = 0.3f + sparkIntensity * 0.15f;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, Timer * 0.12f, sparkleOrigin, sparkScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 碎魂黄泉雷的分裂子雷
    /// </summary>
    public class SoulShatteringSubBomb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulShatteringUnderworldBomb";
        private ref float Timer => ref Projectile.ai[0];
        private const int SubFuseTime = 40;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SubFuseTime + 20;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.scale = 0.7f;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity.Y += 0.35f;
            if (Projectile.velocity.Y > 14f) Projectile.velocity.Y = 14f;
            Projectile.rotation += Projectile.velocity.X * 0.06f;

            float fuseProgress = Timer / SubFuseTime;
            Lighting.AddLight(Projectile.Center, 0.6f * fuseProgress, 0.2f * fuseProgress, 0.8f * fuseProgress);

            if (Main.rand.NextBool(2)) {
                Dust fuse = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.PurpleTorch,
                    Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -0.5f),
                    100, default, Main.rand.NextFloat(1f, 1.5f)
                );
                fuse.noGravity = true;
            }
            if (Timer >= SubFuseTime) { SubExplode(); }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { SubExplode(); }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity *= 0f;
            return false;
        }

        private void SubExplode() {
            if (Projectile.timeLeft <= 0) return;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.position -= new Vector2(100, 100);
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0f }, Projectile.Center);
            Vector2 explosionCenter = Projectile.Center;

            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust fire = Dust.NewDustPerfect(explosionCenter, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(2f, 3.5f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust soul = Dust.NewDustPerfect(explosionCenter, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                soul.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi / 12f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 10f);
                Dust ring = Dust.NewDustPerfect(explosionCenter, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(2f, 3.2f));
                ring.noGravity = true;
            }

            Lighting.AddLight(explosionCenter, 2f, 1f, 2.5f);

            // 升级演出: 二段子雷 ElementalScreenTint 染屏 + 层叠冲击环 (仅本机)
            SoulShatterBlastFX.Spawn(Projectile.GetSource_FromThis(), explosionCenter, 1, Projectile.owner);
            WeaponVFX.AddScreenShake(explosionCenter, 4f);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(explosionCenter, npc.Center) < 150f) {
                    npc.AddBuff(BuffID.ShadowFlame, 360);
                    npc.AddBuff(BuffID.OnFire3, 360);
                }
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float fuseProgress = Timer / SubFuseTime;
            Color mainColor = Color.Lerp(lightColor, new Color(200, 140, 255), fuseProgress * 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null && fuseProgress > 0.3f) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float glowIntensity = (fuseProgress - 0.3f) / 0.7f;
                float pulse = 0.3f + MathF.Sin(Timer * 0.4f) * 0.1f;
                Color glowColor = new Color(180, 80, 220) * glowIntensity * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glowColor, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 碎魂两级爆炸演出弹幕 (纯视觉, damage=0)。stage=0 主雷大爆: GenericWarp 虚空冲击扭曲 + 层叠冲击环;
    /// stage=1 子雷二段爆: ElementalScreenTint 短促紫染屏 (≤0.15) + 层叠冲击环。全屏后处理走单一名额; 绘制只在 PreDraw。
    /// </summary>
    public class SoulShatterBlastFX : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 38;
        private int Stage => (int)Projectile.ai[0];

        public static void Spawn(IEntitySource source, Vector2 worldPos, int stage, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulShatterBlastFX>(), 0, 0f, owner, stage);
        }

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
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.15f ? life / 0.15f : 1f - (life - 0.15f) / 0.85f, 0f, 1f);
            bool main = Stage == 0;
            float baseR = main ? 26f : 16f;
            float grow = main ? 320f : 200f;
            SpriteBatch sb = Main.spriteBatch;

            // —— 层叠冲击环 (三环, 相位错开) ——
            for (int r = 0; r < 3; r++) {
                float phase = MathHelper.Clamp(life - r * 0.12f, 0f, 1f);
                if (phase <= 0f) continue;
                float ringFade = MathHelper.Clamp(1f - phase, 0f, 1f) * fade;
                WeaponVFX.DrawShockwaveRing(Projectile.Center, baseR + phase * grow, main ? 18f : 12f, ringFade * 0.85f,
                    new Color(220, 130, 255), new Color(90, 30, 160));
            }

            if (main) {
                // —— GenericWarp 虚空冲击扭曲 (向外推) ——
                Effect warp = ACMShaders.GenericWarp;
                if (warp != null && fade > 0.05f && ACMShaders.RequestFullscreenSlot()) {
                    ACMShaders.SetCommonParams(warp, Projectile.Center, fade);
                    warp.Parameters["uRadius"]?.SetValue(0.5f + life * 0.3f);
                    warp.Parameters["uWarpScale"]?.SetValue(1.6f);
                    warp.Parameters["uChroma"]?.SetValue(0.6f);
                    warp.Parameters["uRadialPull"]?.SetValue(-0.5f); // 向外推(爆炸)
                    warp.Parameters["uMode"]?.SetValue(4f);          // void
                    warp.Parameters["uTint"]?.SetValue(new Vector4(0.35f, 0.16f, 0.5f, 0.6f));
                    ACMShaders.ApplyScreenPostProcess(sb, warp, bindNoise: true);
                }
                if (fade > 0.4f)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.14f, fade * 0.8f, new Color(190, 110, 255), 8f);
            }
            else {
                // —— ElementalScreenTint 短促紫染屏 (≤0.15, 程序化 overlay) ——
                Effect tint = ACMShaders.ElementalScreenTint;
                if (tint != null && fade > 0.05f) {
                    tint.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    tint.Parameters["uIntensity"]?.SetValue(fade * 0.13f);
                    tint.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                    tint.Parameters["uTint"]?.SetValue(new Vector4(0.4f, 0.18f, 0.55f, 0.85f));
                    tint.Parameters["uTint2"]?.SetValue(new Vector4(0.12f, 0.05f, 0.2f, 1f));
                    tint.Parameters["uVignette"]?.SetValue(0.5f);
                    tint.Parameters["uFogScale"]?.SetValue(2.8f);
                    sb.End();
                    ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }

            return false;
        }
    }
}

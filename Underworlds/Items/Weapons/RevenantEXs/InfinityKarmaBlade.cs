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
    /// 孽镜无间回旋刃 - KarmasMirrorBlade的终极升级版
    /// 如同无间地狱般永无止境轮回的利刃，可同时发射3把回旋刃
    /// 特殊机制：三刃齐发、镜像反射伤害加深、暴击时敌人承受双倍回旋伤害
    /// </summary>
    public class InfinityKarmaBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2560;
            Item.crit = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<InfinityKarmaBladeProj>();
            Item.shootSpeed = 22f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            // 三刃齐发：扇形散射
            for (int i = -1; i <= 1; i++) {
                Vector2 rotatedDir = direction.RotatedBy(MathHelper.ToRadians(i * 12));
                Projectile.NewProjectile(source, player.Center + rotatedDir * 25f, rotatedDir * Item.shootSpeed, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<InfinityKarmaBladeProj>()] < 3;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<KarmasMirrorBlade>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class InfinityKarmaBladeProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/InfinityKarmaBlade";

        private enum BladeState { Flying, Returning }
        private BladeState State {
            get => (BladeState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float Timer => ref Projectile.ai[1];
        private const float MaxDistance = 900f;
        private const float ReturnSpeed = 30f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) { Projectile.Kill(); return; }
            Timer++;
            Projectile.rotation += 0.5f * Projectile.direction;

            switch (State) {
                case BladeState.Flying: HandleFlying(owner); break;
                case BladeState.Returning: HandleReturning(owner); break;
            }
            SpawnInfinityParticles();
            Lighting.AddLight(Projectile.Center, 1f, 0.8f, 1.2f);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.975f;
            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 2f || Timer > 50) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.5f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);
            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 2f, 1f - distance / MaxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.18f);
            if (distance < 40f) { Projectile.Kill(); }
        }

        private void SpawnInfinityParticles() {
            for (int i = 0; i < 2; i++) {
                Dust mirror = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 15, 30, 30, DustID.SilverCoin,
                    Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f,
                    60, default, Main.rand.NextFloat(1.2f, 2f)
                );
                mirror.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(18, 18), 4, 4, DustID.Shadowflame,
                    0, 0, 120, default, Main.rand.NextFloat(1.0f, 1.8f)
                );
                shadow.noGravity = true;
                shadow.velocity = -Projectile.velocity * 0.3f;
            }
            if (Main.rand.NextBool(3)) {
                Dust glint = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.WhiteTorch,
                    Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f),
                    40, default, 1f
                );
                glint.noGravity = true;
            }
            // 无间轮回的残影粒子
            if (Main.rand.NextBool(2)) {
                Dust karma = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(20, 20), 4, 4, DustID.PurpleTorch,
                    0f, -0.5f, 80, default, 1.5f
                );
                karma.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 无间镜像反伤：暴击时反射额外伤害
            if (hit.Crit) {
                target.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.6f }, target.Center);
                // 镜像破碎效果
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                    Dust shatter = Dust.NewDustPerfect(target.Center, DustID.WhiteTorch, vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
                    shatter.noGravity = true;
                }
            }

            target.AddBuff(BuffID.Ichor, 300);
            target.AddBuff(BuffID.BrokenArmor, 300);

            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.SilverCoin, vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
                burst.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                Dust shadow = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(1.5f, 2.2f));
                shadow.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == BladeState.Flying) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f }, Projectile.position);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(160, 80, 230), new Color(240, 230, 255), progress) * progress * 0.6f;
                trailColor.A = 0;
                float scale = Projectile.scale * progress;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
            }

            Color mainColor = Color.Lerp(lightColor, new Color(240, 230, 255), 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.35f + MathF.Sin(Timer * 0.2f) * 0.1f;
                Color starColor = new Color(220, 210, 255) * 0.6f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.12f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null) {
                Vector2 sparkleOrigin = sparkle.Size() / 2f;
                float sparkPulse = 0.3f + MathF.Sin(Timer * 0.3f) * 0.08f;
                Color sparkColor = new Color(200, 180, 255) * 0.4f;
                sparkColor.A = 0;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, -Timer * 0.1f, sparkleOrigin, sparkPulse, SpriteEffects.None, 0);
            }

            Color glowColor = new Color(200, 190, 240) * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 18; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.SilverCoin,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    60, default, Main.rand.NextFloat(1.2f, 2f)
                );
                death.noGravity = true;
            }
        }
    }
}

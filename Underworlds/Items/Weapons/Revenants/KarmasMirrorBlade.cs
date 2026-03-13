using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 孽镜回旋刃 - 如同孽镜台般映照罪孽、去而复返的利刃，回旋镖类武器
    /// 肉后中期，投掷后旋转飞行并返回，命中敌人时映照其罪孽产生镜面碎裂特效
    /// </summary>
    public class KarmasMirrorBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 62;
            Item.crit = 10;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<KarmasMirrorBladeProj>();
            Item.shootSpeed = 16f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            direction = direction.RotatedByRandom(MathHelper.ToRadians(2f));
            Projectile.NewProjectile(source, player.Center + direction * 20f, direction * Item.shootSpeed, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<KarmasMirrorBladeProj>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 孽镜回旋刃弹幕 - 旋转飞出后返回玩家，带有镜面反射拖尾
    /// 使用ACMAsset.Sparkle叠加命中火花，ACMAsset.BlankStar绘制镜光
    /// </summary>
    public class KarmasMirrorBladeProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/KarmasMirrorBlade";

        private enum BladeState { Flying, Returning }
        private BladeState State {
            get => (BladeState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private const float MaxDistance = 500f;
        private const float ReturnSpeed = 20f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Timer++;

            //高速旋转
            Projectile.rotation += 0.35f * Projectile.direction;

            switch (State) {
                case BladeState.Flying:
                    HandleFlying(owner);
                    break;
                case BladeState.Returning:
                    HandleReturning(owner);
                    break;
            }

            SpawnMirrorParticles();

            //冷白色+淡紫色光照（镜面反射感）
            Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0.7f);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.97f;

            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 2f || Timer > 40) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);

            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.5f, 1f - distance / MaxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.15f);

            if (distance < 35f) {
                Projectile.Kill();
            }
        }

        private void SpawnMirrorParticles() {
            //镜面碎光粒子
            if (Main.rand.NextBool(2)) {
                Dust mirror = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 12,
                    24, 24, DustID.SilverCoin,
                    Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                mirror.noGravity = true;
            }

            //暗紫冥气
            if (Main.rand.NextBool(3)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.Shadowflame,
                    0, 0, 150, default, Main.rand.NextFloat(0.6f, 1.0f)
                );
                shadow.noGravity = true;
                shadow.velocity = -Projectile.velocity * 0.2f;
            }

            //白色闪光碎片
            if (Main.rand.NextBool(5)) {
                Dust glint = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.WhiteTorch,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f),
                    60, default, 0.6f
                );
                glint.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //孽镜映照：暴击时复制一次伤害（镜像反射）
            if (hit.Crit && Main.rand.NextBool(3)) {
                target.SimpleStrikeNPC(damageDone / 3, hit.HitDirection, false, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
            }

            //附加减防
            target.AddBuff(BuffID.Ichor, 150);

            //镜面碎裂爆发特效
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.SilverCoin, vel,
                    60, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                burst.noGravity = true;
            }

            //暗紫爆发
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust shadow = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                shadow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == BladeState.Flying) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f }, Projectile.position);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            //镜面反射拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                //银白色到暗紫色渐变
                Color trailColor = Color.Lerp(new Color(120, 80, 180), new Color(220, 220, 255), progress) * progress * 0.5f;
                trailColor.A = 0;
                float scale = Projectile.scale * progress;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
            }

            //绘制主体
            Color mainColor = Color.Lerp(lightColor, new Color(230, 220, 255), 0.3f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //使用BlankStar叠加镜面星光
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.25f + MathF.Sin(Timer * 0.15f) * 0.08f;
                Color starColor = new Color(200, 200, 255) * 0.4f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.1f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            //使用Sparkle叠加碎裂光线
            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null) {
                Vector2 sparkleOrigin = sparkle.Size() / 2f;
                float sparkPulse = 0.2f + MathF.Sin(Timer * 0.25f) * 0.05f;
                Color sparkColor = new Color(180, 160, 220) * 0.3f;
                sparkColor.A = 0;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, -Timer * 0.08f, sparkleOrigin, sparkPulse, SpriteEffects.None, 0);
            }

            //镜面光晕
            Color glowColor = new Color(180, 170, 220) * 0.3f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.SilverCoin,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    80, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                death.noGravity = true;
            }
        }
    }
}

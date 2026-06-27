using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 冥府幽火铳 - 发射冥府幽火的火器，远程火铳类武器
    /// 肉后中期，发射散射幽火弹丸，命中产生冥烟爆裂
    /// </summary>
    public class NetherfireBlunderbuss : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 42;
            Item.crit = 6;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 54;
            Item.height = 22;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherfireBullet>();
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Bullet;
            Item.staff[Type] = true;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-8, 2);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int netherfireBullet = ModContent.ProjectileType<NetherfireBullet>();

            //散射3-5发幽火弹丸
            int count = Main.rand.Next(3, 6);
            for (int i = 0; i < count; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(12));
                perturbedSpeed *= Main.rand.NextFloat(0.8f, 1.2f);
                Projectile.NewProjectile(source, position, perturbedSpeed, netherfireBullet, damage, knockback, player.whoAmI);
            }

            //枪口冥烟特效
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 30f;
            for (int i = 0; i < 15; i++) {
                Vector2 smokeVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(35)) * Main.rand.NextFloat(2f, 6f);
                Dust smoke = Dust.NewDustPerfect(
                    muzzlePos, DustID.Smoke,
                    smokeVel, 180,
                    new Color(80, 40, 120), Main.rand.NextFloat(1.2f, 2.0f)
                );
                smoke.noGravity = true;
            }

            //枪口幽火闪光
            for (int i = 0; i < 8; i++) {
                Vector2 sparkVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(25)) * Main.rand.NextFloat(3f, 8f);
                Dust spark = Dust.NewDustPerfect(
                    muzzlePos, DustID.PurpleTorch,
                    sparkVel, 100, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                spark.noGravity = true;
            }

            //枪口径向辉光闪 (青黄魂火, 走专属一次性枪口闪弹, 更新阶段安全)
            NetherfireMuzzleFlash.Spawn(source, muzzlePos, muzzleDir, player.whoAmI);

            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //枪口位置调整
            position += velocity.SafeNormalize(Vector2.Zero) * 20f;
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
    /// 幽火弹丸弹幕 - 带有冥火拖尾的散射弹丸，命中时产生冥烟爆裂
    /// 表现重做: 双层短拖尾 (<see cref="WeaponVFX.DrawProjectileTrail"/>) + LightShot 光弹核;
    /// 命中走 <see cref="ACMWeaponBurst"/> 青黄魂火演出。
    /// </summary>
    public class NetherfireBullet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/NetherfireBlunderbuss";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //轻微重力
            Projectile.velocity.Y += 0.05f;

            //冥紫色光照
            Lighting.AddLight(Projectile.Center, 0.4f, 0.15f, 0.5f);

            //幽火拖尾
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity,
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    120, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                flame.noGravity = true;
            }

            //暗烟拖尾
            if (Main.rand.NextBool(3)) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.Smoke,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    180, new Color(60, 30, 90), 0.8f
                );
                smoke.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冥火灼烧
            target.AddBuff(BuffID.ShadowFlame, 120);
            target.AddBuff(BuffID.OnFire3, 90);

            //命中冥烟爆裂
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                burst.noGravity = true;
            }

            //冥烟扩散
            for (int i = 0; i < 4; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(2f, 2f);
                Dust smoke = Dust.NewDustPerfect(
                    target.Center, DustID.Smoke, smokeVel,
                    200, new Color(80, 40, 120), Main.rand.NextFloat(1.5f, 2.5f)
                );
                smoke.noGravity = true;
            }

            //命中演出: 青黄魂火径向辉光 + 冲击环 (走 ACMWeaponBurst, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.SoulFire, scale: 0.7f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            //双层短拖尾 (外宽暗冥紫 + 内窄亮幽火)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
                outerColor: new Color(70, 30, 110, 140), innerColor: new Color(200, 110, 240, 190),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            //使用LightShot灰度图绘制幽火光弹核心
            Texture2D lightShot = ACMAsset.LightShot;
            if (lightShot != null) {
                Vector2 origin = lightShot.Size() / 2f;

                //主体光弹
                Color mainColor = new Color(200, 100, 255) * 0.8f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, 0.5f, SpriteEffects.None, 0);

                //外层光晕
                Color glowColor = new Color(140, 50, 200) * 0.4f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, 0.7f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);

            //消亡冥火碎片
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }

            //冥烟
            for (int i = 0; i < 4; i++) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, -1f,
                    200, new Color(80, 40, 120), Main.rand.NextFloat(1.0f, 1.8f)
                );
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 幽火铳·枪口闪弹 (纯视觉, damage=0): 开火瞬间在枪口跑一道青黄魂火
    /// <see cref="WeaponVFX.DrawRadialBloom"/> 闪光 + <see cref="ACMShaders.DrawBeam"/> 短热浪锥。
    /// 绘制只在 PreDraw, 开火阶段仅 <see cref="Spawn"/> 触发 (仅 owner 客户端)。
    /// </summary>
    public class NetherfireMuzzleFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 10;

        private ref float DirX => ref Projectile.ai[0];
        private ref float DirY => ref Projectile.ai[1];

        public static void Spawn(IEntitySource source, Vector2 worldPos, Vector2 dir, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<NetherfireMuzzleFlash>(), 0, 0f, owner, dir.X, dir.Y);
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

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.6f, 0.55f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life; // 0→1
            float fade = MathHelper.Clamp(1f - life, 0f, 1f);     // 开火最亮, 快速衰减

            Vector2 dir = new Vector2(DirX, DirY);
            if (dir == Vector2.Zero)
                dir = Vector2.UnitX;
            dir.Normalize();

            //短热浪锥 (BeamGrad), 沿枪口方向
            ACMShaders.DrawBeam(Projectile.Center - dir * 6f, Projectile.Center + dir * (28f + fade * 18f),
                halfWidth: 9f * fade + 2f, core: new Color(255, 230, 150), edge: new Color(120, 220, 200),
                intensity: fade * 0.9f, flowSpeed: 3.5f, flowScale: 2.6f, coreSharp: 2.4f);

            //枪口径向辉光 (青黄魂火, 走全屏名额, 名额满退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, radiusFrac: 0.025f + fade * 0.02f,
                intensity: fade * 0.55f, color: new Color(255, 225, 140), rayCount: 6f);

            return false;
        }
    }
}

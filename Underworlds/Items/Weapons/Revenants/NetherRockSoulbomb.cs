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
    /// 冥岩爆魂雷 - 由冥岩制成、能引爆灵魂的雷弹，投掷炸弹类武器
    /// 肉后中期，投掷后延时爆炸，产生大范围冥火和灵魂碎片
    /// </summary>
    public class NetherRockSoulbomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 78;
            Item.crit = 4;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 28;
            Item.height = 28;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<NetherRockSoulbombProj>();
            Item.shootSpeed = 9f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
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
    /// 冥岩爆魂雷弹幕 - 抛物线飞行的冥岩雷弹，接触敌人或延时后爆炸
    /// 使用ACMAsset.Sparkle叠加爆炸线条，ACMAsset.EmberShards绘制碎片飞散
    /// </summary>
    public class NetherRockSoulbombProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/NetherRockSoulbomb";

        private ref float Timer => ref Projectile.ai[0];
        private ref float HasBounced => ref Projectile.ai[1];
        private const int FuseTime = 90;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
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

            //重力
            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 14f) Projectile.velocity.Y = 14f;

            //旋转
            Projectile.rotation += Projectile.velocity.X * 0.04f;

            //引信闪烁光照（越接近爆炸越亮）
            float fuseProgress = Timer / FuseTime;
            float flicker = MathF.Sin(Timer * (0.3f + fuseProgress * 0.5f)) * 0.5f + 0.5f;
            Lighting.AddLight(Projectile.Center, 0.5f * flicker * fuseProgress, 0.2f * flicker * fuseProgress, 0.6f * flicker * fuseProgress);

            //引信冥火粒子
            if (Main.rand.NextBool(3)) {
                Dust fuse = Dust.NewDustDirect(
                    Projectile.Center + new Vector2(0, -Projectile.height * 0.4f),
                    4, 4, DustID.PurpleTorch,
                    Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -0.5f),
                    100, default, Main.rand.NextFloat(0.7f, 1.2f)
                );
                fuse.noGravity = true;
            }

            //接近爆炸时冒冥烟
            if (fuseProgress > 0.6f && Main.rand.NextBool(3)) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.Center, 6, 6, DustID.Smoke,
                    0f, -1f, 200, new Color(80, 40, 120), Main.rand.NextFloat(0.8f, 1.3f)
                );
                smoke.noGravity = true;
            }

            //达到引信时间爆炸
            if (Timer >= FuseTime) {
                Explode();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //接触敌人立即爆炸
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹一次
            if (HasBounced == 0) {
                HasBounced = 1;
                if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.5f;
                if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
                Projectile.velocity *= 0.6f;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
                return false;
            }
            //第二次碰撞停在地上
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        private void Explode() {
            if (Projectile.timeLeft <= 0) return;

            //设置爆炸范围伤害
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.position -= new Vector2(80, 80);
            Projectile.width = 160;
            Projectile.height = 160;
            Projectile.Damage();

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.3f }, Projectile.Center);

            Vector2 explosionCenter = Projectile.Center;

            //冥火爆裂粒子
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust fire = Dust.NewDustPerfect(
                    explosionCenter, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(2.0f, 3.0f)
                );
                fire.noGravity = true;
            }

            //灵魂碎片飞散
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                vel.Y -= 2f;
                Dust soul = Dust.NewDustPerfect(
                    explosionCenter, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                soul.noGravity = true;
            }

            //冥烟蘑菇云
            for (int i = 0; i < 15; i++) {
                Vector2 smokeVel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f));
                Dust smoke = Dust.NewDustPerfect(
                    explosionCenter, DustID.Smoke, smokeVel,
                    200, new Color(80, 40, 120), Main.rand.NextFloat(2.0f, 3.5f)
                );
                smoke.noGravity = true;
            }

            //暗影焰环
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi / 16f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(5f, 9f);
                Dust ring = Dust.NewDustPerfect(
                    explosionCenter, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.8f, 2.5f)
                );
                ring.noGravity = true;
            }

            //爆炸光照
            Lighting.AddLight(explosionCenter, 1.5f, 0.8f, 2f);

            //附近敌人附加减益
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(explosionCenter, npc.Center) < 120f) {
                    npc.AddBuff(BuffID.ShadowFlame, 240);
                    npc.AddBuff(BuffID.OnFire3, 180);
                }
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            //绘制主体
            float fuseProgress = Timer / FuseTime;
            Color mainColor = Color.Lerp(lightColor, new Color(200, 150, 255), fuseProgress * 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //引信光晕（越接近爆炸越强烈）
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null && fuseProgress > 0.3f) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float glowIntensity = (fuseProgress - 0.3f) / 0.7f;
                float pulse = 0.4f + MathF.Sin(Timer * (0.3f + fuseProgress * 0.4f)) * 0.15f;
                Color glowColor = new Color(180, 80, 220) * glowIntensity * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glowColor, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            //使用Sparkle叠加闪烁光纹
            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null && fuseProgress > 0.5f) {
                Vector2 sparkleOrigin = sparkle.Size() / 2f;
                float sparkIntensity = (fuseProgress - 0.5f) / 0.5f;
                Color sparkColor = new Color(200, 100, 255) * sparkIntensity * 0.3f;
                sparkColor.A = 0;
                float sparkScale = 0.2f + sparkIntensity * 0.1f;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, Timer * 0.1f, sparkleOrigin, sparkScale, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}

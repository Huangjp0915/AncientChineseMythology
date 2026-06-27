using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 阎摩断业刀 - 阎王用以裁断众生业报的巨刀，近战大刀类武器
    /// 肉后中期，挥舞释放断业剑气弹幕，击中敌人时产生业火爆裂特效
    /// </summary>
    public class YamasSeverance : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 86;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.scale = 1.25f;
            Item.shoot = ModContent.ProjectileType<YamasSeveranceSlash>();
            Item.shootSpeed = 12f;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生暗紫色冥火粒子
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Shadowflame,
                    player.velocity.X * 0.3f, player.velocity.Y * 0.3f,
                    100, default, 1.4f
                );
                flame.noGravity = true;
            }
            //幽魂拖影
            if (Main.rand.NextBool(3)) {
                Dust soul = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith,
                    0f, -1f, 150, default, 1.1f
                );
                soul.noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //断业之焰：附加暗影焰和地狱火
            target.AddBuff(BuffID.ShadowFlame, 180);
            target.AddBuff(BuffID.OnFire3, 180);

            //击中爆裂特效
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                burst.noGravity = true;
            }

            //暴击时产生断业冲击波
            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.3f }, target.Center);
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    Dust ring = Dust.NewDustPerfect(
                        target.Center, DustID.PurpleTorch, vel,
                        80, default, Main.rand.NextFloat(1.8f, 2.5f)
                    );
                    ring.noGravity = true;
                }

                //断业重击演出: 冥紫径向辉光 + 冲击环 + 轻屏震
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 1.4f, owner: player.whoAmI);
                WeaponVFX.AddScreenShake(target.Center, 3.5f);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //每次挥舞释放一道断业剑气
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
            position = player.Center + direction * 40f;
            Projectile.NewProjectile(source, position, velocity, type, (int)(damage * 0.7f), knockback * 0.5f, player.whoAmI);
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
    /// 断业剑气弹幕 - 紫色剑气波，向前飞行并穿透敌人
    /// 使用ACMAsset.GlaciateWave灰度图叠加绘制
    /// </summary>
    public class YamasSeveranceSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/YamasSeverance";

        private int hitCount;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 45;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 80;
        }

        public override void AI() {
            //逐渐消退
            Projectile.alpha += 4;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //冥紫色光照
            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 0.5f * brightness, 0.2f * brightness, 0.6f * brightness);

            //剑气粒子
            if (Main.rand.NextBool(2)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(10, 10),
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    120, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                trail.noGravity = true;
            }

            //散落冥紫碎片
            if (Main.rand.NextBool(4)) {
                Dust shard = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.PurpleTorch,
                    0f, -0.5f, 100, default, 0.9f
                );
                shard.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                burst.noGravity = true;
            }

            //第三次贯穿命中触发"宽幅断业"径向辉光大爆 (穿透 4)
            hitCount++;
            if (hitCount == 3) {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 1.8f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 4f);
                SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.6f, Pitch = -0.2f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate == null)
                return false;

            Vector2 origin = glaciate.Size() / 2f;
            float opacity = (255 - Projectile.alpha) / 255f;
            Vector2 screenCenter = Projectile.Center - Main.screenPosition;

            //外层光晕底
            Color glowColor = new Color(140, 60, 200) * opacity * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(glaciate, screenCenter, null, glowColor, Projectile.rotation, origin, new Vector2(0.5f, 0.35f), SpriteEffects.None, 0);

            //主体剑气: DissolveBurn 噪声消融 (随 alpha 上升而灼烧崩解)
            float threshold = MathHelper.Clamp(1f - opacity, 0f, 1f);
            WeaponVFX.ApplyDissolveBurn(glaciate, Projectile.Center, null,
                new Color(200, 120, 255) * 0.9f, Projectile.rotation, origin, 0.42f,
                threshold: threshold, intensity: opacity,
                edgeColor: new Color(235, 130, 255, 200), edgeWidth: 0.1f, noiseScale: 2.2f,
                direction: -Projectile.velocity.SafeNormalize(Vector2.UnitX), sweepStrength: 0.6f);

            //BeamGrad 扇形断业弧光边 (横切剑气, 体现刀锋利刃)
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            float arcHalf = 64f;
            ACMShaders.DrawBeam(Projectile.Center - perp * arcHalf, Projectile.Center + perp * arcHalf,
                halfWidth: 10f, core: new Color(225, 160, 255), edge: new Color(120, 40, 190),
                intensity: opacity, flowSpeed: 1.8f, flowScale: 1.6f, coreSharp: 2.8f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }
        }
    }
}

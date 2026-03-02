using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
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
        }

        public override bool PreDraw(ref Color lightColor) {
            //使用GlaciateWave灰度图绘制剑气效果
            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate != null) {
                Vector2 origin = glaciate.Size() / 2f;
                float opacity = (255 - Projectile.alpha) / 255f;

                //绘制拖尾
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(180, 80, 220), new Color(100, 30, 150), 1f - progress) * progress * opacity * 0.5f;
                    trailColor.A = 0;
                    float scale = 0.35f * progress;
                    Main.EntitySpriteDraw(glaciate, drawPos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(scale, scale * 0.5f), SpriteEffects.None, 0);
                }

                //绘制主体剑气
                Color mainColor = new Color(200, 120, 255) * opacity * 0.8f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, new Vector2(0.4f, 0.25f), SpriteEffects.None, 0);

                //外层光晕
                Color glowColor = new Color(140, 60, 200) * opacity * 0.4f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, new Vector2(0.5f, 0.35f), SpriteEffects.None, 0);
            }

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

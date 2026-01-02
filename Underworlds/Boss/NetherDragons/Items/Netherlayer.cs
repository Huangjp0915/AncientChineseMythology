using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥刃 - 近战武器，继承幽冥龙的虚空斩击特性
    /// </summary>
    internal class Netherlayer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 180;
            Item.DamageType = DamageClass.Melee;
            Item.width = 88;
            Item.height = 88;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.shoot = ModContent.ProjectileType<NetherSlashProjectile>();
            Item.shootSpeed = 1f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 每次挥砍发射一道虚空斩波
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);

            Projectile.NewProjectile(source, player.Center, direction * 16f, type,
                (int)(damage * 1.2f), knockback, player.whoAmI);

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥砍时产生幽冥粒子
            if (Main.rand.NextBool(3)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width),
                                             hitbox.Y + Main.rand.Next(hitbox.Height));

                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = player.velocity * 0.5f;
            }
        }

        public override void AddRecipes() {
            // TODO: 添加合成配方
        }
    }

    /// <summary>
    /// 虚空斩波弹幕 - 纯粒子效果
    /// </summary>
    public class NetherSlashProjectile : ModProjectile
    {
        private const int MaxParticles = 50;
        private ParticleData[] particles = new ParticleData[MaxParticles];

        private struct ParticleData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float Scale;
            public float Rotation;
            public Color Color;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            // 旋转
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                InitializeParticles();
                Projectile.ai[0] = 1;
            }

            UpdateParticles();

            // 轻微减速
            Projectile.velocity *= 0.98f;

            // 每帧补充新粒子
            if (Main.rand.NextBool(2)) {
                SpawnParticle(Projectile.Center + Main.rand.NextVector2Circular(30, 30));
            }
        }

        private void InitializeParticles() {
            for (int i = 0; i < MaxParticles; i++) {
                SpawnParticle(Projectile.Center);
            }
        }

        private void SpawnParticle(Vector2 position) {
            for (int i = 0; i < MaxParticles; i++) {
                if (particles[i].Life <= 0) {
                    float angle = Projectile.rotation + Main.rand.NextFloat(-0.3f, 0.3f);
                    Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(2f, 8f);

                    particles[i] = new ParticleData {
                        Position = position,
                        Velocity = velocity,
                        Life = 1f,
                        Scale = Main.rand.NextFloat(1f, 2f),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        Color = Main.rand.NextBool() ? new Color(100, 150, 255) : new Color(150, 200, 255)
                    };
                    break;
                }
            }
        }

        private void UpdateParticles() {
            for (int i = 0; i < MaxParticles; i++) {
                if (particles[i].Life > 0) {
                    particles[i].Position += particles[i].Velocity;
                    particles[i].Velocity *= 0.95f;
                    particles[i].Life -= 0.04f;
                    particles[i].Rotation += 0.1f;
                    particles[i].Scale *= 0.98f;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 扩大碰撞检测
            Rectangle expandedHitbox = new Rectangle(
                projHitbox.X - 40,
                projHitbox.Y - 40,
                projHitbox.Width + 80,
                projHitbox.Height + 80
            );

            return expandedHitbox.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中时爆发粒子
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(target.position, target.width, target.height,
                    DustID.BlueTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(8, 8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 绘制粒子系统
            for (int i = 0; i < MaxParticles; i++) {
                if (particles[i].Life > 0) {
                    Vector2 drawPos = particles[i].Position - Main.screenPosition;
                    float alpha = particles[i].Life;
                    Color drawColor = particles[i].Color * alpha * 0.8f;

                    // 使用多层粒子创建发光效果
                    for (int j = 0; j < 3; j++) {
                        float layerScale = particles[i].Scale * (1f + j * 0.3f);
                        float layerAlpha = alpha * (1f - j * 0.3f);

                        int dust = Dust.NewDustPerfect(particles[i].Position, DustID.BlueTorch,
                            Vector2.Zero, 100, drawColor * layerAlpha, layerScale).dustIndex;
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].rotation = particles[i].Rotation;
                    }
                }
            }

            // 绘制核心发光
            Vector2 corePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 5; i++) {
                Vector2 offset = new Vector2(MathF.Cos(Main.GlobalTimeWrappedHourly * 4f + i),
                                            MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i)) * (5f + i * 2f);
                Color coreColor = new Color(150, 200, 255, 0) * 0.6f;

                for (int j = 0; j < 3; j++) {
                    Dust.NewDustPerfect(Projectile.Center + offset, DustID.BlueTorch,
                        Vector2.Zero, 100, coreColor, 2f + j * 0.5f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.8f, 1.5f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消散时粒子爆发
            for (int i = 0; i < 30; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(10, 10);
            }
        }
    }
}


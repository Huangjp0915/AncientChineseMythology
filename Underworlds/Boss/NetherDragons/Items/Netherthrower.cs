using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥喷射器 - 远程武器，喷射幽冥龙息
    /// </summary>
    internal class Netherthrower : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 95;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 64;
            Item.height = 32;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item34;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<NetherBreathProjectile>();
            Item.shootSpeed = 16f;
            Item.noMelee = true;
            Item.useAmmo = AmmoID.Gel; // 使用凝胶作为弹药
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            // 转换为幽冥龙息
            type = ModContent.ProjectileType<NetherBreathProjectile>();
            
            // 添加随机扩散
            velocity = velocity.RotatedByRandom(0.15f) * Main.rand.NextFloat(0.9f, 1.1f);
            
            // 从枪口位置发射
            position += velocity.SafeNormalize(Vector2.Zero) * 50f;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-10f, -2f);
        }

        public override void AddRecipes()
        {
            // TODO: 添加合成配方
        }
    }

    /// <summary>
    /// 幽冥龙息弹幕 - 纯粒子火焰效果
    /// </summary>
    public class NetherBreathProjectile : ModProjectile
    {
        private const int MaxParticles = 30;
        private List<FlameParticle> particles = new List<FlameParticle>();
        
        private class FlameParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public Color BaseColor;
            
            public FlameParticle(Vector2 pos, Vector2 vel)
            {
                Position = pos;
                Velocity = vel;
                MaxLife = Main.rand.NextFloat(0.5f, 1f);
                Life = MaxLife;
                Scale = Main.rand.NextFloat(0.8f, 1.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                
                // 幽冥火焰颜色：深蓝到浅蓝渐变
                float colorMix = Main.rand.NextFloat();
                BaseColor = Color.Lerp(new Color(80, 120, 255), new Color(150, 200, 255), colorMix);
            }
            
            public void Update()
            {
                Position += Velocity;
                Velocity *= 0.96f;
                Life -= 0.02f;
                Rotation += 0.15f;
                Scale *= 0.99f;
            }
            
            public float Alpha => Life / MaxLife;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // 受重力影响
            Projectile.velocity.Y += 0.15f;
            
            // 轻微减速
            Projectile.velocity *= 0.98f;
            
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 持续生成火焰粒子
            for (int i = 0; i < 2; i++)
            {
                Vector2 offset = Main.rand.NextVector2Circular(10, 10);
                Vector2 particleVel = Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(2, 2);
                particles.Add(new FlameParticle(Projectile.Center + offset, particleVel));
            }

            // 更新并移除死亡粒子
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update();
                if (particles[i].Life <= 0)
                {
                    particles.RemoveAt(i);
                }
            }

            // 限制粒子数量
            while (particles.Count > MaxParticles)
            {
                particles.RemoveAt(0);
            }

            // 环境粒子
            if (Main.rand.NextBool(2))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.BlueTorch, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f, 
                    100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 燃烧Debuff
            target.AddBuff(BuffID.OnFire, 180);
            
            // 击中爆发粒子
            for (int i = 0; i < 15; i++)
            {
                Vector2 particleVel = Main.rand.NextVector2Circular(5, 5);
                particles.Add(new FlameParticle(target.Center, particleVel));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // 绘制火焰粒子
            foreach (var particle in particles)
            {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                float alpha = particle.Alpha;
                
                // 多层叠加创造火焰效果
                for (int i = 0; i < 4; i++)
                {
                    float layerProgress = i / 4f;
                    float layerScale = particle.Scale * (1.5f - layerProgress * 0.5f);
                    float layerAlpha = alpha * (1f - layerProgress * 0.6f);
                    
                    // 颜色从核心（亮）到边缘（暗）渐变
                    Color layerColor = Color.Lerp(Color.White, particle.BaseColor, layerProgress);
                    layerColor *= layerAlpha;
                    
                    int dustType = i % 2 == 0 ? DustID.BlueTorch : DustID.BlueFairy;
                    int dust = Dust.NewDustPerfect(particle.Position, dustType, 
                        Vector2.Zero, 0, layerColor, layerScale).dustIndex;
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].rotation = particle.Rotation;
                }
            }

            // 绘制核心高亮
            Vector2 corePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 3; i++)
            {
                float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.3f;
                Color coreColor = new Color(200, 220, 255, 0) * 0.8f;
                
                int dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueFairy, 
                    Vector2.Zero, 0, coreColor, 1.5f + i * 0.5f * pulseScale).dustIndex;
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 1.0f, 1.8f);
            
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // 消散粒子
            for (int i = 0; i < 20; i++)
            {
                Vector2 particleVel = Main.rand.NextVector2Circular(6, 6);
                particles.Add(new FlameParticle(Projectile.Center, particleVel));
            }
            
            SoundEngine.PlaySound(SoundID.Item74, Projectile.position);
        }
    }
}


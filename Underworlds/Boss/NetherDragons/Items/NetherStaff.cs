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
    /// 幽冥杖 - 魔法武器，召唤幽冥能量球
    /// </summary>
    internal class NetherStaff : ModItem
    {
        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true; // 使物品使用时显示为法杖动画
        }

        public override void SetDefaults()
        {
            Item.damage = 135;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<NetherOrbProjectile>();
            Item.shootSpeed = 12f;
            Item.mana = 15;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 在鼠标位置生成能量球
            Vector2 spawnPos = Main.MouseWorld + Main.rand.NextVector2Circular(30, 30);
            
            Projectile.NewProjectile(source, spawnPos, Vector2.Zero, type, damage, knockback, player.whoAmI);
            
            return false;
        }

        public override void AddRecipes()
        {
            // TODO: 添加合成配方
        }
    }

    /// <summary>
    /// 幽冥能量球 - 追踪敌人的纯粒子弹幕
    /// </summary>
    public class NetherOrbProjectile : ModProjectile
    {
        private const int MaxCoreParticles = 40;
        private const int MaxTrailParticles = 60;
        
        private List<OrbParticle> coreParticles = new List<OrbParticle>();
        private List<TrailParticle> trailParticles = new List<TrailParticle>();
        
        private class OrbParticle
        {
            public Vector2 LocalPosition; // 相对于能量球中心的位置
            public float OrbitRadius;
            public float OrbitAngle;
            public float OrbitSpeed;
            public float Life;
            public float Scale;
            public Color Color;
            
            public OrbParticle()
            {
                OrbitRadius = Main.rand.NextFloat(10f, 35f);
                OrbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                OrbitSpeed = Main.rand.NextFloat(0.05f, 0.15f) * Main.rand.NextBool().ToDirectionInt();
                Life = 1f;
                Scale = Main.rand.NextFloat(0.8f, 1.5f);
                
                float colorMix = Main.rand.NextFloat();
                Color = Color.Lerp(new Color(100, 150, 255), new Color(150, 200, 255), colorMix);
            }
            
            public void Update()
            {
                OrbitAngle += OrbitSpeed;
                LocalPosition = new Vector2(MathF.Cos(OrbitAngle), MathF.Sin(OrbitAngle)) * OrbitRadius;
                Life -= 0.015f;
                Scale *= 0.99f;
            }
        }
        
        private class TrailParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float Scale;
            public Color Color;
            
            public TrailParticle(Vector2 pos)
            {
                Position = pos;
                Velocity = Main.rand.NextVector2Circular(2, 2);
                Life = 1f;
                Scale = Main.rand.NextFloat(0.5f, 1.2f);
                Color = new Color(100, 150, 255);
            }
            
            public void Update()
            {
                Position += Velocity;
                Velocity *= 0.95f;
                Life -= 0.03f;
                Scale *= 0.97f;
            }
        }

        private NPC targetNPC;
        private float hoverTime = 0f;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // 初始化粒子
            if (Projectile.ai[0] == 0)
            {
                for (int i = 0; i < MaxCoreParticles; i++)
                {
                    coreParticles.Add(new OrbParticle());
                }
                Projectile.ai[0] = 1;
                
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, Projectile.Center);
            }

            hoverTime++;

            // 寻找并追踪敌人
            if (targetNPC == null || !targetNPC.active || targetNPC.life <= 0)
            {
                targetNPC = FindClosestEnemy(800f);
            }

            if (targetNPC != null)
            {
                // 追踪敌人
                Vector2 toTarget = targetNPC.Center - Projectile.Center;
                float distance = toTarget.Length();
                
                if (distance > 50f)
                {
                    Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 18f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.05f);
                }
                else
                {
                    // 接近目标时减速
                    Projectile.velocity *= 0.9f;
                }
            }
            else
            {
                // 没有目标时轻微漂浮
                float hoverX = MathF.Sin(hoverTime * 0.05f) * 2f;
                float hoverY = MathF.Cos(hoverTime * 0.03f) * 2f;
                Projectile.velocity = new Vector2(hoverX, hoverY);
            }

            // 更新核心粒子
            for (int i = coreParticles.Count - 1; i >= 0; i--)
            {
                coreParticles[i].Update();
                if (coreParticles[i].Life <= 0)
                {
                    coreParticles[i] = new OrbParticle();
                }
            }

            // 生成拖尾粒子
            if (Projectile.velocity.Length() > 2f)
            {
                trailParticles.Add(new TrailParticle(Projectile.Center));
            }

            // 更新拖尾粒子
            for (int i = trailParticles.Count - 1; i >= 0; i--)
            {
                trailParticles[i].Update();
                if (trailParticles[i].Life <= 0)
                {
                    trailParticles.RemoveAt(i);
                }
            }

            // 限制拖尾粒子数量
            while (trailParticles.Count > MaxTrailParticles)
            {
                trailParticles.RemoveAt(0);
            }

            Projectile.rotation += 0.1f;
        }

        private NPC FindClosestEnemy(float maxDistance)
        {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.CanBeChasedBy() && !npc.friendly)
                {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 击中时爆发粒子
            for (int i = 0; i < 30; i++)
            {
                int dust = Dust.NewDust(target.position, target.width, target.height, 
                    DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(10, 10);
            }
            
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // 绘制拖尾粒子
            foreach (var particle in trailParticles)
            {
                float alpha = particle.Life;
                Color drawColor = particle.Color * alpha * 0.6f;
                
                for (int i = 0; i < 2; i++)
                {
                    int dust = Dust.NewDustPerfect(particle.Position, DustID.BlueTorch, 
                        Vector2.Zero, 100, drawColor, particle.Scale * (1f + i * 0.3f)).dustIndex;
                    Main.dust[dust].noGravity = true;
                }
            }

            // 绘制核心粒子
            foreach (var particle in coreParticles)
            {
                Vector2 worldPos = Projectile.Center + particle.LocalPosition;
                float alpha = particle.Life;
                
                // 多层渲染
                for (int i = 0; i < 3; i++)
                {
                    float layerScale = particle.Scale * (1.2f + i * 0.4f);
                    float layerAlpha = alpha * (1f - i * 0.3f);
                    Color layerColor = particle.Color * layerAlpha;
                    
                    int dustType = i == 0 ? DustID.BlueFairy : DustID.BlueTorch;
                    int dust = Dust.NewDustPerfect(worldPos, dustType, 
                        Vector2.Zero, 0, layerColor, layerScale).dustIndex;
                    Main.dust[dust].noGravity = true;
                }
            }

            // 绘制能量球核心（脉冲效果）
            Vector2 corePos = Projectile.Center - Main.screenPosition;
            float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.4f;
            
            for (int i = 0; i < 5; i++)
            {
                Vector2 offset = new Vector2(MathF.Cos(Projectile.rotation + i * MathHelper.TwoPi / 5f),
                                            MathF.Sin(Projectile.rotation + i * MathHelper.TwoPi / 5f)) * (8f * pulseScale);
                
                Color coreColor = new Color(200, 255, 255, 0) * 0.9f;
                
                for (int j = 0; j < 3; j++)
                {
                    int dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.BlueFairy,
                        Vector2.Zero, 0, coreColor, 2f + j * 0.5f).dustIndex;
                    Main.dust[dust].noGravity = true;
                }
            }

            // 绘制能量环
            float ringRadius = 35f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 5f;
            int ringSegments = 24;
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = MathHelper.TwoPi * i / ringSegments;
                Vector2 ringPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                
                Color ringColor = new Color(150, 180, 255, 0) * 0.7f;
                int dust = Dust.NewDustPerfect(ringPos, DustID.BlueTorch, 
                    Vector2.Zero, 0, ringColor, 1.5f).dustIndex;
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 1.0f, 2.0f);
            
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // 爆炸效果
            for (int i = 0; i < 50; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(12, 12);
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.BlueTorch, velocity.X, velocity.Y, 100, default, 3f);
                Main.dust[dust].noGravity = true;
            }
            
            SoundEngine.PlaySound(SoundID.Item62, Projectile.position);
        }
    }
}


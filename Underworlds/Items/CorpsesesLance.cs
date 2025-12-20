using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items
{
    /// <summary>
    /// 枉死千骸之枪 - 继承Boss的IK手臂追踪特性
    /// </summary>
    internal class CorpsesesLance : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 165;
            Item.DamageType = DamageClass.Melee;
            Item.width = 72;
            Item.height = 72;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CorpsesesLanceProj>();
            Item.shootSpeed = 1f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 生成追踪长枪弹幕
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes()
        {
            // TODO: 添加合成配方
        }
    }

    /// <summary>
    /// 枉死千骸长枪弹幕 - 使用IK系统自动追踪敌人
    /// </summary>
    public class CorpsesesLanceProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/CorpsesesLance";
        private const float MaxReach = 400f; // 最大伸展距离
        private const float RetractSpeed = 25f; // 回收速度
        
        private Vector2 anchorPos; // 锚点位置（玩家手部）
        private Vector2 tipPos; // 枪尖位置
        private NPC targetNPC; // 锁定目标
        
        private enum LanceState
        {
            Extending,   // 伸展
            Attacking,   // 攻击
            Retracting   // 回收
        }
        
        private LanceState State
        {
            get => (LanceState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        
        private ref float ExtendProgress => ref Projectile.ai[1];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // 更新锚点位置
            anchorPos = player.Center;

            switch (State)
            {
                case LanceState.Extending:
                    HandleExtending(player);
                    break;
                case LanceState.Attacking:
                    HandleAttacking(player);
                    break;
                case LanceState.Retracting:
                    HandleRetracting(player);
                    break;
            }

            // 更新弹幕位置和旋转
            Projectile.Center = tipPos;
            Projectile.rotation = (tipPos - anchorPos).ToRotation();
        }

        private void HandleExtending(Player player)
        {
            ExtendProgress += 0.08f;
            
            // 寻找最近的敌人
            if (targetNPC == null || !targetNPC.active)
            {
                targetNPC = FindClosestEnemy(player.Center, 600f);
            }

            Vector2 targetDir;
            if (targetNPC != null)
            {
                // 追踪敌人
                targetDir = (targetNPC.Center - anchorPos).SafeNormalize(Vector2.Zero);
            }
            else
            {
                // 朝鼠标方向
                targetDir = (Main.MouseWorld - anchorPos).SafeNormalize(Vector2.Zero);
            }

            float currentLength = MathHelper.Lerp(0, MaxReach, ExtendProgress);
            tipPos = anchorPos + targetDir * currentLength;

            if (ExtendProgress >= 1f)
            {
                State = LanceState.Attacking;
                ExtendProgress = 0f;
                SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
            }
        }

        private void HandleAttacking(Player player)
        {
            ExtendProgress += 0.05f;

            // 保持伸展状态，轻微追踪
            if (targetNPC != null && targetNPC.active)
            {
                Vector2 toTarget = (targetNPC.Center - anchorPos).SafeNormalize(Vector2.Zero);
                Vector2 currentDir = (tipPos - anchorPos).SafeNormalize(Vector2.Zero);
                Vector2 newDir = Vector2.Lerp(currentDir, toTarget, 0.1f).SafeNormalize(Vector2.Zero);
                tipPos = anchorPos + newDir * MaxReach;
            }

            if (ExtendProgress >= 1f)
            {
                State = LanceState.Retracting;
                ExtendProgress = 0f;
            }
        }

        private void HandleRetracting(Player player)
        {
            Vector2 toAnchor = anchorPos - tipPos;
            float distance = toAnchor.Length();

            if (distance > 10f)
            {
                tipPos += toAnchor.SafeNormalize(Vector2.Zero) * RetractSpeed;
            }
            else
            {
                Projectile.Kill();
            }
        }

        private NPC FindClosestEnemy(Vector2 position, float maxDistance)
        {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.CanBeChasedBy() && !npc.friendly)
                {
                    float dist = Vector2.Distance(npc.Center, position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // 线段碰撞检测
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), 
                anchorPos, tipPos, 16f, ref point);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() / 2f;

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(150, 50, 200) * progress * 0.5f;
                
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, 
                    Projectile.oldRot[i] + MathHelper.PiOver4, drawOrigin, Projectile.scale * 0.9f, SpriteEffects.None);
            }

            // 绘制连接线（模拟手臂）
            DrawArmConnection(anchorPos, tipPos, lightColor);

            // 绘制主体
            Color mainColor = Color.Lerp(lightColor, new Color(180, 80, 255), 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, 
                mainColor, Projectile.rotation + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None);

            return false;
        }

        private void DrawArmConnection(Vector2 start, Vector2 end, Color baseColor)
        {
            Vector2 diff = end - start;
            float rotation = diff.ToRotation();
            float length = diff.Length();

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle(0, 0, 1, 1);
            Vector2 origin = new Vector2(0, 0.5f);
            Vector2 scale = new Vector2(length, 6f);

            Color lineColor = new Color(100, 50, 150, 100);
            Main.EntitySpriteDraw(pixel, start - Main.screenPosition, rect, 
                lineColor, rotation, origin, scale, SpriteEffects.None);
        }
    }
}


using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.Projectiles
{
    class GemStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GemStickSpearProjectile";
        private bool isReturning = false;// 是否正在返回
        private bool isEnd = false;// 是否结束
        private bool isNext = false;
        private Color swingColor;

        public override void SetDefaults()
        {
            Projectile.width = 142;
            Projectile.height = 142;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.scale = 1.2f;
            Projectile.alpha = 0;
            Projectile.ownerHitCheck = true;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
        }
        private Player Owner => Main.player[Projectile.owner];
        public override void OnSpawn(IEntitySource source)
        {
            // 随机选择颜色
            Color[] colors = { Color.Red * 0.8f, Color.Green * 1.6f, Color.Blue, Color.Gold * 0.8f, Color.Purple, Color.White * 0.5f };
            swingColor = colors[Main.rand.Next(colors.Length)];
        }

        private void MoveToTarget(Vector2 target)// 移动到目标位置
        {
            Vector2 move = target - Projectile.Center;
            float distance = move.Length();
            move.Normalize();
            move *= distance / 30f + 48f;
            Projectile.velocity = move;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            // 随机选择粒子类型
            int[] dustTypes = { DustID.RedTorch, DustID.BubbleBurst_Blue, DustID.Poisoned, DustID.MagicMirror, DustID.GoldFlame, DustID.Shadowflame };
            int selectedDustType = dustTypes[Main.rand.Next(dustTypes.Length)];

            Projectile.direction = player.direction;
            player.heldProj = Projectile.whoAmI;// 玩家持有弹道

            if (Main.mouseRight && !isNext)
            {
                Projectile.timeLeft = 30;
                if(!isReturning)
                {
                    // 停止移动以便旋转
                    Projectile.velocity = Vector2.Zero;
                    Projectile.Center = player.Center;
                    Projectile.knockBack = Projectile.knockBack * 0.99f;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                    Projectile.rotation - MathHelper.ToRadians(-45f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
                }
                if (Main.mouseLeft)
                    isReturning = true;
            }
            if(!isReturning && !Main.mouseRight || isNext)
            {
                Projectile.width = 30;
                Projectile.height = 30;
                if(!isNext)
                {
                    Projectile.Center = player.Center;
                    isNext = true;
                }
                //Projectile.timeLeft = 13;
                for (int i = 0; i < 2; i++)
                {
                    int dust = Dust.NewDust(player.Center, 10, 10, selectedDustType, 0, 0, 1, swingColor, 1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.2f;
                }
                if (!isEnd)
                MoveToTarget(Main.MouseWorld); // 移动到玩家位置
                if (Projectile.Distance(Main.MouseWorld) < 40f)
                {
                    isEnd = true;
                    MoveToTarget(player.Center);
                    
                }
            }
            if (Projectile.Distance(player.Center) < 60f && isEnd)
            {
                Projectile.Kill(); // 销毁弹道
            }
            if (isReturning && !isNext)
            {
                Projectile.Center = Main.MouseWorld;
                Projectile.knockBack = Projectile.knockBack * 0.99f;
                if (!Main.mouseRight)
                {
                    player.immune = true;// 玩家无敌
                    player.immuneTime = 30; // 确保无敌时间短于冲刺持续时间

                    // 瞬移玩家到弹幕位置
                    player.Teleport(Projectile.Center, 12);
                    for (int i = 0; i < 60; i++) // 创建50个粒子
                    {
                        // 使用 Main.dust 来创建粒子
                        Dust dust = Dust.NewDustPerfect(player.Center, selectedDustType, Main.rand.NextVector2Unit() * 12f, 1, swingColor, 1f);
                        dust.noGravity = true; // 使粒子无重力，保持在空中
                        dust.noLight = true; // 无光照
                        dust.scale = 2f; // 设置粒子大小
                    }
                    Projectile.Kill(); // 销毁弹幕
                }
            }
            if (player.direction == 1)// 玩家朝向右侧
            {
                if (isNext)
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4; // 左右旋转
                else
                    Projectile.rotation += 0.4f; // 左右旋转
            }
            else
            {
                if (isNext)
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4; // 左右旋转
                else
                    Projectile.rotation -= 0.4f; // 左右旋转
            }
            
            // 计算右上角位置并生成粒子
            Vector2 dustOffset = new Vector2(60, -60);
            Vector2 rotatedDustOffset = dustOffset.RotatedBy(Projectile.rotation);
            Vector2 dustPosition = Projectile.Center + rotatedDustOffset - new Vector2(8, 5);

            int dust_1 = Dust.NewDust(dustPosition, 10, 10, selectedDustType, 0, 0, 1, swingColor, 1f);
            Main.dust[dust_1].noGravity = true;
            Main.dust[dust_1].velocity *= 0.2f;
            Main.dust[dust_1].scale = 1.2f;
            Main.dust[dust_1].alpha = 100;

            Vector2 dustOffset_2 = new Vector2(-60, 60);
            Vector2 rotatedDustOffset_2 = dustOffset_2.RotatedBy(Projectile.rotation);
            Vector2 dustPosition_2 = Projectile.Center + rotatedDustOffset_2 - new Vector2(8, 5);

            int dust_2 = Dust.NewDust(dustPosition_2, 10, 10, selectedDustType, 0, 0, 1, swingColor, 1f);
            Main.dust[dust_2].noGravity = true;
            Main.dust[dust_2].velocity *= 0.2f;
            Main.dust[dust_2].scale = 1.2f;
            Main.dust[dust_2].alpha = 100;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            if (isNext)
            {
                modifiers.FinalDamage *= 2f;
            }
            if (Main.mouseRight && !isNext)
            {
                if(isReturning)
                    modifiers.FinalDamage *= 0.25f;
                else
                    modifiers.FinalDamage *= 0.5f;
            }
        }
        [Obsolete]
        public override void OnKill(int timeLeft)
        {
            Player player = Main.player[Projectile.owner];
            player.velocity *= 0.8f;
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = new Rectangle(
                0,
                texture.Height / Main.projFrames[Type] * Projectile.frame,
                texture.Width,
                texture.Height / Main.projFrames[Type]
            );

            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / Main.projFrames[Type] / 2); // 设置原点为中心
            Main.EntitySpriteDraw(
                texture, // 第一个参数是材质
                Projectile.Center - Main.screenPosition,
                rectangle, // 第三个参数是帧图选框
                Color.White, // 第四个参数是颜色
                Projectile.rotation, // 第五个参数是贴图旋转方向
                origin,
                Projectile.scale * 1.2f, // 第七个参数是缩放
                SpriteEffects.None,
                0);
            return false;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent; // 用于 TextureAssets

namespace AncientChineseMythology.Projectiles
{
    public class PigCharmLaser : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/PigCharmLaser";
        
        // 定义状态枚举：0=发射，1=持续，2=结束
        private enum LaserState
        {
            Firing = 0,
            Continuous = 1,
            Ending = 2
        }

        // 三个贴图路径
        private const string FiringTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Firing";
        private const string ContinuousTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Continuous";
        private const string EndingTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Ending";

        // 每个动画状态的帧数（均为8帧）
        private const int FiringFrameCount = 8;
        private const int ContinuousFrameCount = 8;
        private const int EndingFrameCount = 8;
        // 每帧持续时间（每隔 FrameDuration 帧切换动画帧）
        private const int FrameDuration = 5;

        public override void SetStaticDefaults()
        {
            // 初始状态使用发射动画的帧数
            Main.projFrames[Projectile.type] = FiringFrameCount;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;  // 初始尺寸，实际碰撞框在 AI 中更新
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1; // 命中一个敌人后结束
            // 设为 false，因为我们手动检测地形阻挡
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3600;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
            // 初始状态为 Firing
            Projectile.localAI[0] = (int)LaserState.Firing;
            // 用于记录扣魔力计时（单位帧），初始为0
            Projectile.ai[0] = 0f;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player.dead)
            {
                Projectile.Kill();
                return;
            }

            // 添加蓝色亮光
            Lighting.AddLight(Projectile.Center, new Vector3(0f, 0f, 1f));

            // 计算玩家中心到鼠标的理想距离与方向
            Vector2 origin = player.Center;
            Vector2 diff = Main.MouseWorld - origin;
            float idealDistance = diff.Length();
            if (idealDistance < 0.0001f)
            {
                idealDistance = 0.0001f;
                diff = Vector2.UnitY;
            }
            else
            {
                diff.Normalize();
            }

            // 地形阻挡检测：逐步检测，若遇到实心砖块，则截断激光
            float step = 8f; // 检测步长
            float effectiveDistance = idealDistance;
            for (float d = 0; d < idealDistance; d += step)
            {
                Vector2 checkPos = origin + diff * d;
                if (Collision.SolidCollision(checkPos, 1, 1))
                {
                    effectiveDistance = d - 1f;
                    break;
                }
            }
            if (effectiveDistance < 8f)
                effectiveDistance = 8f;
            Projectile.ai[1] = effectiveDistance;

            // 在激光沿途添加蓝色光照
            float lightStep = 16f;
            for (float d = 0; d < effectiveDistance; d += lightStep)
            {
                Vector2 lightPos = origin + diff * d;
                Lighting.AddLight(lightPos, new Vector3(0.2f, 0.4f, 1f));
            }
            Vector2 endPos = origin + diff * effectiveDistance;
            Lighting.AddLight(endPos, new Vector3(0.2f, 0.4f, 1f));

            // 设置激光位置、旋转
            Projectile.rotation = diff.ToRotation() - MathHelper.PiOver2;
            Projectile.position = origin;
            Projectile.velocity = Vector2.Zero;

            // 状态切换处理
            LaserState state = (LaserState)(int)Projectile.localAI[0];
            if (state == LaserState.Firing)
            {
                if (!player.channel)
                {
                    EnterEndingState();
                }
                else
                {
                    // 每次AI调用增加0.5f
                    Projectile.ai[0] += 0.5f;
                    if (Projectile.ai[0] >= 60f)
                    {
                        Projectile.ai[0] = 0f;
                        if (player.statMana >= 10)
                        {
                            player.statMana -= 10;
                        }
                        else
                        {
                            EnterEndingState();
                        }
                    }
                    
                    if (Projectile.frame >= FiringFrameCount - 1 && Projectile.frameCounter >= FrameDuration - 1)
                    {
                        EnterContinuousState();
                    }
                }
            }
            else if (state == LaserState.Continuous)
            {
                if (!player.channel)
                {
                    EnterEndingState();
                }
                else
                {
                    Projectile.ai[0] += 0.5f;
                    if (Projectile.ai[0] >= 60f)
                    {
                        Projectile.ai[0] = 0f;
                        if (player.statMana >= 10)
                        {
                            player.statMana -= 10;
                        }
                        else
                        {
                            EnterEndingState();
                        }
                    }
                }
            }
            else if (state == LaserState.Ending)
            {
                if (Projectile.frame >= EndingFrameCount - 1 && Projectile.frameCounter >= FrameDuration - 1)
                {
                    Projectile.Kill();
                    return;
                }
            }

            // 更新碰撞区域：高度 = effectiveDistance, 宽度固定（10像素）
            Projectile.width = 10;
            Projectile.height = (int)effectiveDistance;

            UpdateAnimationFrames();
        }

        private void UpdateAnimationFrames()
        {
            LaserState state = (LaserState)(int)Projectile.localAI[0];
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= FrameDuration)
            {
                Projectile.frameCounter = 0;
                if (state == LaserState.Continuous)
                {
                    // 持续状态循环播放
                    Projectile.frame = (Projectile.frame + 1) % ContinuousFrameCount;
                }
                else if (state == LaserState.Firing)
                {
                    Projectile.frame++;
                    if (Projectile.frame >= FiringFrameCount)
                    {
                        EnterContinuousState();
                    }
                }
                else if (state == LaserState.Ending)
                {
                    Projectile.frame++;
                    if (Projectile.frame >= EndingFrameCount)
                    {
                        Projectile.frame = EndingFrameCount - 1;
                    }
                }
            }
        }

        private void EnterContinuousState()
        {
            Projectile.localAI[0] = (int)LaserState.Continuous;
            Projectile.frame = 0;
            Projectile.frameCounter = 0;
            Main.projFrames[Projectile.type] = ContinuousFrameCount;
        }

        private void EnterEndingState()
        {
            if ((LaserState)(int)Projectile.localAI[0] != LaserState.Ending)
            {
                Projectile.localAI[0] = (int)LaserState.Ending;
                Projectile.frame = 0;
                Projectile.frameCounter = 0;
                Main.projFrames[Projectile.type] = EndingFrameCount;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            Vector2 start = player.Center;
            float effectiveDistance = Projectile.ai[1];
            Vector2 end = start + (Main.MouseWorld - start).SafeNormalize(Vector2.UnitY) * effectiveDistance;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 10f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            float effectiveDistance = Projectile.ai[1];
            LaserState state = (LaserState)(int)Projectile.localAI[0];
            string texturePath = FiringTexturePath;
            int frameCount = FiringFrameCount;
            if (state == LaserState.Continuous)
            {
                texturePath = ContinuousTexturePath;
                frameCount = ContinuousFrameCount;
            }
            else if (state == LaserState.Ending)
            {
                texturePath = EndingTexturePath;
                frameCount = EndingFrameCount;
            }

            Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
            int frameHeight = texture.Height / frameCount;
            Rectangle frame = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);

            // 绘制原点设为贴图上方中点，使贴图的顶部始终对齐玩家中心
            Vector2 drawOrigin = new Vector2(frame.Width / 2f, 0f);
            float scale = effectiveDistance / frameHeight;
            if (scale < 0.1f)
                scale = 0.1f;

            Main.EntitySpriteDraw(
                texture,
                player.Center - Main.screenPosition,
                frame,
                Color.White,
                Projectile.rotation,
                drawOrigin,
                scale,
                SpriteEffects.None,
                0
            );
            return false;
        }
    }
}

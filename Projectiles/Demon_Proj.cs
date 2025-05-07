using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class Demon_Proj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/Demon_Proj";

        public override void SetStaticDefaults()
        {
            // 指定该投射物共有20帧
            Main.projFrames[Projectile.type] = 20;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16; 
            Projectile.height = 16; 
            Projectile.aiStyle = 0; 
            Projectile.friendly = false;    // 敌方投射物
            Projectile.hostile = true;      // 对玩家有效
            Projectile.penetrate = 1;       // 碰撞一次后消失
            Projectile.tileCollide = true;  // 碰到地形后消失
            Projectile.ignoreWater = true;  
            Projectile.timeLeft = 300;      // 最大存在时间
        }

        public override void AI()
        {
            // 投射物沿当前速度直线飞行，无需额外运动逻辑

            // 动画处理：每6个 tick 切换一帧
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                // 当动画播放完20帧后自动消失
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                {
                    Projectile.Kill();
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // 碰到地形时直接消失
            Projectile.Kill();
            return false;
        }
    }
}

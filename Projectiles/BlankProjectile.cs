using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class BlankProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile"; // 弹幕贴图
        public override void SetDefaults() {
            Projectile.width = 10; // 弹幕宽度
            Projectile.height = 10; // 弹幕高度
            Projectile.friendly = true; // 友方弹幕
            Projectile.tileCollide = false; // 不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Default; // 伤害类型
            Projectile.penetrate = 1; // 穿透
            Projectile.ignoreWater = true; // 无视液体
            Projectile.timeLeft = 10; // 存在时间，单位为帧
            Projectile.alpha = 255; // 透明度
            Projectile.light = 0f; // 发光亮度
        }
        public override void OnSpawn(IEntitySource source)// 弹幕生成时调用
        {
            Player player = Main.player[Projectile.owner];
            SoundEngine.PlaySound(SoundID.Item1, player.position); // 使用音效
            Projectile.damage = 0;
        }

        public override void AI() {
            //获取玩家
            Player player = Main.player[Projectile.owner];
            //获取玩家位置
            Vector2 playerPos = player.position;
            //不移动
            Projectile.position = playerPos;
        }
    }


}

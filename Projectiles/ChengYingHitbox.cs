using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles;

public class ChengYingHitbox : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";
    public override void SetDefaults() {
        Projectile.width  = 96;   // = your sword textureWidth
        Projectile.height = 34;   // = textureHeight
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate   = -1;      // ∞  次数
        Projectile.timeLeft    = 2;       // 让 AI 每帧刷新
        Projectile.hide        = true;    // 不渲染
        Projectile.DamageType  = DamageClass.Melee; // or Generic
    }

    public override void AI() {
        Player player = Main.player[Projectile.owner];
        // 定位到坐骑中心（略微后移让剑尖判定）
        Projectile.Center = player.Center + 
            new Vector2(-24 * player.direction, 0f);
        Projectile.direction = player.direction;
        Projectile.timeLeft = 2;          // 永不过期
    }
}

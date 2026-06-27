using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 鱼肠剑·穿心技 (右键) — 可见质变 (纯表现): 突进宝石光束 + 青紫双层拖尾,
    /// 命中触发 <see cref="ACMWeaponBurst"/> 宝石爆发。机制/伤害不变。
    /// </summary>
    public class YuChangSkillProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordProjectile";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;                  //碰撞箱宽度
            Projectile.height = 50;                 //碰撞箱高度
            Projectile.friendly = true;             //对玩家友好
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;              //无限穿透敌人
            Projectile.timeLeft = 60;               //存在 1 秒后自动消失
            Projectile.aiStyle = 0;                 //自定义 AI
            Projectile.tileCollide = true;          //与方块碰撞时消失，如需穿墙设置 false

        }

        public override void AI() {
            //偏转315°始终保持朝向飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation()
                + MathHelper.PiOver2
                + MathHelper.ToRadians(315f);

            //添加拖尾粒子效果
            Vector2 dustPos = Projectile.Center;
            Dust d = Dust.NewDustDirect(
                dustPos - new Vector2(2, 2), //左上微调，使尘粒正好落在中心
                4, 4,                        //区域大小 4×4
                DustID.WhiteTorch,           //白色火炬尘，也可换成 DustID.PureWhite
                0f, 0f,                      //初始速度 0
                100,                         //透明度
                default(Color),              //默认颜色
                1.2f                         //缩放
            );
            d.noGravity = true;               //无重力
            d.velocity = Projectile.velocity * 0.15f; //轻微跟随飞行方向

        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞方块时销毁投射物
            Projectile.Kill();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gem, scale: 1.4f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 突进宝石光束 + 青紫双层拖尾 (纯表现), return true 保留原版刃身绘制
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 42f, Projectile.Center + dir * 42f, 14f,
                new Color(170, 120, 255), new Color(150, 230, 255), 0.9f);
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(95, 60, 185, 150), innerColor: new Color(150, 230, 255, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
            return true;
        }
    }
}
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 索魂匕 - 地府索魂使者的双匕首，投掷/近战武器
    /// 肉后初期，可投掷，攻速快，有几率造成即死（对普通敌人）
    /// </summary>
    public class SoulseekerDaggers : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 38; //基础伤害
            Item.crit = 12; //高暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 32; //物品宽度
            Item.height = 32; //物品高度
            Item.useTime = 15; //快速使用
            Item.useAnimation = 15; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 2f; //低击退
            Item.value = Item.buyPrice(gold: 4, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.shoot = ProjectileID.BoneGloveProj; //投掷骨头弹幕作为替代
            Item.shootSpeed = 14f; //投掷速度
            Item.noMelee = false; //可以近战
            Item.noUseGraphic = false; //显示使用图形
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //索魂一击：低血量敌人有几率被索命
            if (target.life < target.lifeMax * 0.15f && !target.boss && Main.rand.NextBool(5)) {
                //对非Boss低血量敌人造成致命伤害
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                //产生索魂特效
                for (int i = 0; i < 10; i++) {
                    Dust.NewDust(target.position, target.width, target.height,
                        DustID.Wraith, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.5f);
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //有几率投掷两把匕首
            if (Main.rand.NextBool(3)) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(10));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return true;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生暗色粒子
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(new Microsoft.Xna.Framework.Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith, 0f, 0f, 150, default, 0.8f);
            }
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 噬魂枪 - 吞噬亡魂的地府长枪，近战长矛类武器
    /// 肉后初期，击杀敌人有几率恢复生命
    /// </summary>
    public class SoulDevourerSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 52; //基础伤害
            Item.crit = 5; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 48; //物品宽度
            Item.height = 48; //物品高度
            Item.useTime = 26; //使用时间
            Item.useAnimation = 26; //使用动画时间
            Item.useStyle = ItemUseStyleID.Thrust; //刺击风格
            Item.knockBack = 4f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.scale = 1.3f; //放大显示
            Item.shoot = ProjectileID.None; //不发射弹幕
            Item.shootSpeed = 0f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //噬魂效果：攻击时有几率吸取灵魂恢复生命
            if (Main.rand.NextBool(4)) {
                int healAmount = Main.rand.Next(5, 12);
                player.Heal(healAmount);
                //产生灵魂吸取特效
                for (int i = 0; i < 5; i++) {
                    Dust.NewDust(target.position, target.width, target.height,
                        DustID.Wraith, 0f, -2f, 100, default, 1.2f);
                }
            }
            //给敌人附加暗影焰
            target.AddBuff(BuffID.ShadowFlame, 120); //2秒暗影焰
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //刺击时产生幽灵粒子
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Microsoft.Xna.Framework.Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith, player.velocity.X * 0.3f, player.velocity.Y * 0.3f, 100, default, 1.0f);
            }
        }
    }
}

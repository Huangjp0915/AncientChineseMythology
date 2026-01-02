using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 阎罗锤 - 地府阎王的审判之锤，近战锤类武器
    /// 肉后初期，高击退，攻击附带地狱火debuff
    /// </summary>
    public class YamasGavel : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 58; //基础伤害
            Item.crit = 4; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 48; //物品宽度
            Item.height = 48; //物品高度
            Item.useTime = 35; //使用时间（锤子较慢）
            Item.useAnimation = 35; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 8f; //高击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.scale = 1.1f; //略微放大
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //阎罗审判：附带地狱火效果
            target.AddBuff(BuffID.OnFire, 180); //3秒地狱火
            //有几率造成混乱
            if (Main.rand.NextBool(5)) {
                target.AddBuff(BuffID.Confused, 120); //2秒混乱
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生火焰粒子
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Microsoft.Xna.Framework.Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Torch, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 100, default, 1.5f);
            }
        }
    }
}

using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 血魔巨剑 - 地府血魔锻造的巨剑，近战大剑类武器
    /// 肉后初期，攻击吸血，范围较大
    /// </summary>
    public class BloodfiendGreatsword : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 155; //基础伤害
            Item.crit = 8; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 64; //物品宽度（大剑较大）
            Item.height = 64; //物品高度
            Item.useTime = 12; //使用时间（大剑较慢）
            Item.useAnimation = 12; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 5.5f; //击退
            Item.value = Item.buyPrice(gold: 5, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.scale = 1.2f; //放大显示
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //血魔吸血：造成伤害的5%转化为生命
            int healAmount = (int)(damageDone * 0.05f);
            if (healAmount > 0) {
                player.Heal(healAmount);
            }
            //暴击时额外吸血
            if (hit.Crit) {
                player.Heal(healAmount);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生血红色粒子
            if (Main.rand.NextBool(2)) {
                Dust.NewDust(new Microsoft.Xna.Framework.Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Blood, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 150, default, 1.4f);
            }
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 刑天之斧 - 无头战神刑天的战斧，近战斧类武器
    /// 肉后初期，攻击速度中等，伤害较高，有穿甲效果
    /// </summary>
    public class AxeofXingTian : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 62; //基础伤害
            Item.crit = 6; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 56; //物品宽度
            Item.height = 56; //物品高度
            Item.useTime = 28; //使用时间
            Item.useAnimation = 28; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 6f; //击退
            Item.value = Item.buyPrice(gold: 6); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.scale = 1.15f; //放大显示
            Item.ArmorPenetration = 10; //穿甲效果
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //刑天之怒：低血量时伤害提升
            if (player.statLife < player.statLifeMax2 * 0.5f) {
                target.AddBuff(BuffID.Ichor, 120); //2秒破甲
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生血红色粒子
            if (Main.rand.NextBool(2)) {
                Dust.NewDust(new Microsoft.Xna.Framework.Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Blood, player.velocity.X * 0.3f, player.velocity.Y * 0.3f, 100, default, 1.2f);
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //刑天不屈：血量越低伤害越高（最高+30%）
            float healthRatio = (float)player.statLife / player.statLifeMax2;
            if (healthRatio < 0.5f) {
                damage += 0.3f * (1f - healthRatio * 2f);
            }
        }
    }
}

using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 刑天之斧 - 无头战神刑天的战斧，近战斧类武器
    /// 肉后初期，攻击速度中等，伤害较高，有穿甲效果
    /// 重做：把"刑天不屈"低血狂暴做成可见层 —— 低血时斧刃缠绕血色能量 (节流 LethalRed 演出),
    /// 命中 <see cref="ACMWeaponBurst"/> 致命红辉光 + 屏震 (狂暴态规模放大)。
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

        private static bool IsBerserk(Player player) => player.statLife < player.statLifeMax2 * 0.5f;

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            bool berserk = IsBerserk(player);
            //刑天之怒：低血量时破甲
            if (berserk) {
                target.AddBuff(BuffID.Ichor, 120); //2秒破甲
            }

            //命中演出: 狂暴态致命红辉光放大, 常态青黄魂火
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                berserk ? ACMWeaponBurst.LethalRed : ACMWeaponBurst.SoulFire,
                scale: berserk ? 1.5f : 1.0f, owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, berserk ? 4f : 2.5f);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            bool berserk = IsBerserk(player);
            Vector2 center = hitbox.Center.ToVector2();

            //挥舞血红色粒子 (低血更密更狂)
            if (Main.rand.NextBool(berserk ? 1 : 2)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Blood, player.velocity.X * 0.3f, player.velocity.Y * 0.3f, 100, default,
                    berserk ? 1.5f : 1.2f);
                d.noGravity = berserk;
                if (berserk)
                    d.velocity *= 0.6f;
            }

            //狂暴：斧刃缠绕血色能量 (节流生成一次性 LethalRed 演出, 仅本地玩家)
            if (berserk && Main.rand.NextBool(10)) {
                ACMWeaponBurst.Spawn(player.GetSource_Misc("XingTianBerserk"), center,
                    ACMWeaponBurst.LethalRed, scale: 0.5f, owner: player.whoAmI);
            }

            Lighting.AddLight(center, berserk ? 0.7f : 0.35f, berserk ? 0.08f : 0.12f,
                berserk ? 0.1f : 0.05f);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //刑天不屈：血量越低伤害越高（最高+30%）
            float healthRatio = (float)player.statLife / player.statLifeMax2;
            if (healthRatio < 0.5f) {
                damage += 0.3f * (1f - healthRatio * 2f);
            }
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }
}

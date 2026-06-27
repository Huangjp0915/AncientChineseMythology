using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 青铜剑 — 可见质变 (纯表现): 挥砍带青铜暖金尘拖尾, 命中泛 SoftGlow 闪;
    /// 1% 处决触发 <see cref="ACMWeaponBurst"/> 青铜暖金绿爆发。机制/伤害不变。
    /// </summary>
    public class BronzeSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BronzeSword"; //使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Item.damage = 8; //基础伤害
            Item.crit = 15; //爆击率
            Item.DamageType = DamageClass.Melee; //伤害类型
            Item.width = 52; //物品宽度
            Item.height = 52; //物品高度
            Item.useTime = 10; //使用时间
            Item.useAnimation = 10; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //使用风格
            Item.knockBack = 0; //击退
            Item.value = Item.buyPrice(0, 0, 0, 0); //物品价值
            Item.rare = ItemRarityID.Green; //稀有度
            //Item.UseSound = SoundID.Item1; //使用声音
            //Item.useTurn = true; //自动转向
            Item.autoReuse = true; //自动使用
            Item.noUseGraphic = false; //显示使用图标
            Item.shoot = ModContent.ProjectileType<BlankProjectile>(); //射击类型
            Item.shootSpeed = 1f; //射击速度
        }

        // 青铜暖金尘拖尾 (纯表现, 模拟青铜剑气)
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                int type = Main.rand.NextBool(4) ? DustID.GoldFlame : DustID.Copper;
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, type);
                d.noGravity = true;
                d.velocity *= 0.4f;
                d.scale = Main.rand.NextFloat(0.8f, 1.3f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.4f, 0.33f, 0.12f);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.Poisoned)) {
                target.AddBuff(BuffID.Poisoned, 180); //给目标添加中毒状态
            }
            // 命中 SoftGlow 闪 (轻量, 隔次触发省开销)
            if (Main.rand.NextBool(2)) {
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bronze, scale: 0.7f, owner: player.whoAmI);
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            if (Main.rand.NextBool(100)) {
                //秒杀 NPC
                target.life = 0;
                target.HitEffect();
                target.checkDead();
                // 1% 处决: 青铜暖金绿爆发演出
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bronze, scale: 1.3f, owner: player.whoAmI);
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BronzeIngot>(), 18)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

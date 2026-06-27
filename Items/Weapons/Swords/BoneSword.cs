using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 骨剑 — 超快挥速 (useTime 6)。可见质变 (纯表现, 极廉价): 挥砍偶发骨白小尘,
    /// 偶发命中触发轻量 <see cref="ACMWeaponBurst"/> 骨白爆发。机制/伤害不变。
    /// </summary>
    public class BoneSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BoneSword"; //使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Item.damage = 3; //基础伤害
            Item.crit = 5; //爆击率
            Item.DamageType = DamageClass.Melee; //伤害类型
            Item.width = 52; //物品宽度
            Item.height = 52; //物品高度
            Item.useTime = 6; //使用时间
            Item.useAnimation = 6; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //使用风格
            Item.knockBack = 0; //击退
            Item.value = Item.buyPrice(0, 0, 0, 0); //物品价值
            Item.rare = ItemRarityID.Green; //稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            //Item.useTurn = true; //自动转向
            Item.autoReuse = true; //自动使用
            Item.shoot = ModContent.ProjectileType<BlankProjectile>(); //射击类型
            Item.shootSpeed = 16;
            Item.noUseGraphic = false; //显示使用图标
        }

        // 极廉价骨白尘 (挥速极快, 强力门控避免刷屏)
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Bone);
                d.noGravity = true;
                d.velocity *= 0.3f;
                d.scale = Main.rand.NextFloat(0.6f, 0.9f);
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 仅偶发触发, 保持极低开销
            if (Main.rand.NextBool(5)) {
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bone, scale: 0.7f, owner: player.whoAmI);
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Bone>(), 20)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

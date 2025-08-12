using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
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

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Bone>(), 20)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

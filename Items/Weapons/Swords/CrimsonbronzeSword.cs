using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;


namespace AncientChineseMythology.Items.Waapons.Swords
{
    public class CrimsonbronzeSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/CrimsonbronzeSword"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Item.damage = 59; // 基础伤害
            Item.crit = 2; // 爆击率
            Item.DamageType = DamageClass.Melee; // 伤害类型
            Item.width = 74; // 物品宽度
            Item.height = 82; // 物品高度
            Item.useTime = 25; // 使用时间
            Item.useAnimation = 25; // 使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; // 使用风格
            Item.knockBack = 6; // 击退
            Item.value = Item.buyPrice(0, 10, 0, 0); // 物品价值
            Item.rare = ItemRarityID.Green; // 稀有度
            //Item.UseSound = SoundID.Item1; // 使用音效
            Item.autoReuse = true; // 自动使用
            Item.noUseGraphic = false; // 显示使用图标
            Item.noMelee = false;// 是否可以近战
            Item.shoot = ModContent.ProjectileType<BlankProjectile>(); // 射击类型
            Item.shootSpeed = 1f; // 射击速度
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.OnFire)) {
                target.AddBuff(BuffID.OnFire, 180);
            }
        }
        // 启用右键备用功能
        public override bool AltFunctionUse(Player player) {
            return true;
        }
        public override void HoldItem(Player player) {
            //if(Main.mouseRight)
            //{

            //}
            if (player.altFunctionUse == 2) {
                Item.noMelee = true;
                Item.noUseGraphic = true;
                Item.UseSound = null; // 使用音效
                ////Main.NewText("You can't use this item while holding right mouse button.", 175, 75, 255); // 右键提示信息
                //SoundEngine.PlaySound(SoundID.Item1, player.position); // 使用音效
            }
            else {
                Item.noMelee = false;
                Item.noUseGraphic = false;
                //Item.UseSound = SoundID.Item1; // 使用音效
            }
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) // 右键射击
            {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<CrimsonbronzeSwordProj1>(), damage, knockback, Main.myPlayer);
                return false;
            }
            else if (!Main.mouseRight) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<BlankProjectile>(), damage, knockback, Main.myPlayer);
                return false;
            }
            return false; // 返回 false 以防止原始投射物被发射
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Cuprite.Cuprite>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

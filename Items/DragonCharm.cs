using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class DragonCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/DragonCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            Item.scale = 0.4f;
            // 此武器采用举起使用的方式，可根据需要更换 UseStyle
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.damage = 300; // 可根据需要调整伤害
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            // 发射激光弹
            Item.shoot = ModContent.ProjectileType<DragonCharmLaser>();
            Item.shootSpeed = 16f;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Ranged;
        }

        // 每次使用武器时扣除玩家30点生命值
       public override bool? UseItem(Player player)
        {
            int damage = 30; // 固定扣除的生命值
            player.statLife -= damage;
            // 显示红色的伤害文字
            CombatText.NewText(player.Hitbox, Microsoft.Xna.Framework.Color.Red, damage, true);
            // 如果血量扣除后小于等于0，则触发死亡
            if (player.statLife <= 0)
            {
                player.KillMe(PlayerDeathReason.ByCustomReason($"{player.name} 被龙符咒榨干了..."), damage, 0);
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }
}

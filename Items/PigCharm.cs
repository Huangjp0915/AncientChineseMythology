using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class PigCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/PigCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            // 使用举起的使用方式
            Item.useStyle = ItemUseStyleID.HoldUp;
            // 这里的 useTime 和 useAnimation 设置为 1，实际效果由 channel 控制
            Item.useTime = 1;
            Item.useAnimation = 1;
            Item.channel = true; // 支持持续使用（长按）
            Item.noMelee = true;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            // 不直接设 shoot，采用 HoldItem 来判断是否已生成激光
            Item.DamageType = DamageClass.Magic;
            Item.damage = 168;    // 根据需要调整伤害
            Item.knockBack = 2f;
            // 本物品本身不消耗魔力，魔力消耗在激光内控制
        }

        public override void HoldItem(Player player)
        {
            // 如果玩家在按住左键，并且还没有生成该激光，则生成
            if (player.channel)
            {
                if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.PigCharmLaser>()] <= 0)
                {
                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<Projectiles.PigCharmLaser>(),
                        Item.damage,
                        Item.knockBack,
                        player.whoAmI
                    );
                }
            }
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

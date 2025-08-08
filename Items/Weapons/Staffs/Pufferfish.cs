using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Staffs
{
    public class Pufferfish : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Staffs/Pufferfish";
        public override void SetDefaults() {
            Item.damage = 1111;
            Item.crit = 10;
            Item.DamageType = DamageClass.Magic;
            Item.width = 25;
            Item.height = 31;
            Item.useTime = 25; //使用时间为15帧（1秒大约60帧）
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.knockBack = 18;
            Item.value = Item.buyPrice(platinum: 100);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item13;
            Item.autoReuse = true;
            Item.noMelee = true; //无法近战
            //Item.useTurn = true;
            Item.noUseGraphic = false;
            Item.shoot = ModContent.ProjectileType<Projectiles.PufferfishProj1>(); //射击类型
            Item.shootSpeed = 12f; //射击速度

            Item.mana = 2 * 60 / Item.useTime; //每秒60帧，计算每帧消耗的法力值
            Item.scale = 0.5f;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = TextureAssets.Item[Type].Value;//声明本弹幕的材质
            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            spriteBatch.Draw(texture, drawPosition, sourceRectangle, lightColor, rotation,
                new Vector2(texture.Width / 2, texture.Height / 2), scale / 2f, SpriteEffects.None, 0f);

            return false;
        }
    }
}

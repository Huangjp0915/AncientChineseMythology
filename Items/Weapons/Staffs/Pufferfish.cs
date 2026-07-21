using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Staffs
{
    /// <summary>
    /// 河豚鱼 — 老爹商店彩蛋法杖 ("可爱但危险")。
    /// 按住: 河豚喷高压水柱, 越喷越鼓 (憋气抖动)。
    /// 鼓满 (150 帧) 自动、或松手且蓄力 ≥40% 时打出【喷嚏爆刺】: 锥形 5 + 全向 8 根水刺。
    /// 蓄力不足松手则只可爱地漏气。
    /// </summary>
    public class Pufferfish : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Staffs/Pufferfish";

        public override void SetDefaults() {
            Item.damage = 1111; // 彩蛋数字保留
            Item.crit = 10;
            Item.DamageType = DamageClass.Magic;
            Item.width = 25;
            Item.height = 31;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8;
            Item.value = Item.buyPrice(platinum: 100);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item13;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.noUseGraphic = true; // 河豚本体由手持弹幕绘制 (鼓胀动画)
            Item.channel = true;
            Item.shoot = ModContent.ProjectileType<Projectiles.PufferfishProj1>();
            Item.shootSpeed = 1f; // 只取方向
            Item.mana = 6;        // 起手; 引导期由弹幕按 3/20帧 续费 (≈9/s, 与旧版持平)
            Item.scale = 0.5f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.PufferfishProj1>()] < 1;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = TextureAssets.Item[Type].Value;
            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            spriteBatch.Draw(texture, drawPosition, sourceRectangle, lightColor, rotation,
                new Vector2(texture.Width / 2, texture.Height / 2), scale / 2f, SpriteEffects.None, 0f);

            return false;
        }
    }
}

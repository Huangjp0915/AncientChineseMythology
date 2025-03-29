using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using SubworldLibrary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AncientChineseMythology.Subworlds;

namespace AncientChineseMythology.Tiles
{
    public class TeleportationTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = false;
            Main.tileLighted[Type] = true;
            AddMapEntry(new Color(255, 50, 50));
        }

        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (closer)
            {
                foreach (Player player in Main.player)
                {
                    if (player.active && player.Hitbox.Intersects(new Rectangle(i * 16, j * 16, 16, 16)))
                    {
                        // 使用新版SubworldSystem API
                        SubworldSystem.Enter<UnderworldSubworld>();
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, player.Center);
                    }
                }
            }
        }

        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            // 添加光晕效果
            Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition;
            Texture2D glow = ModContent.Request<Texture2D>("Terraria/Images/Extra_89").Value;
            spriteBatch.Draw(
                glow,
                position + new Vector2(8, 8),
                null,
                new Color(255, 100, 100, 0),
                0f,
                glow.Size() / 2,
                0.5f,
                SpriteEffects.None,
                0f
            );
        }
    }
}
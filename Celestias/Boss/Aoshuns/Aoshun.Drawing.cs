using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region 绘制

        /// <summary>
        /// 头部绘制 - 纹理Aoshun.png: 52×140, 2帧, 每帧52×70
        /// 参考AncientWyrmHead: 基于spriteDirection的origin偏移防止转向跳动
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int frameHeight = texture.Height / HeadFrameCount;
            int yPos = frameHeight * NPC.frame.Y;
            Rectangle sourceRectangle = new Rectangle(0, yPos, texture.Width, frameHeight);

            // 参考原型: 基于朝向的origin偏移，防止转向时精灵跳动
            Vector2 origin = NPC.spriteDirection == -1
                ? new Vector2(texture.Width * 0.5f + 10, frameHeight * 0.5f + 16)
                : new Vector2(texture.Width - 10, frameHeight + 16);

            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition,
                sourceRectangle, drawColor, NPC.rotation, origin, NPC.scale, effects, 0);
            return false;
        }

        public override void FindFrame(int frameHeight) {
            // 参考原型: close时切换帧（近距离张嘴）
            if (close) {
                NPC.frame.Y = 1;
            }
            else {
                NPC.frame.Y = 0;
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation;
        }

        #endregion
    }
}

using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    internal partial class Aoyuan
    {
        #region 绘制

        /// <summary>
        /// 头部绘制 - 纹理Aoyuan.png: 112×438, 3帧, 每帧112×146
        /// 非攻击状态使用NPC.frame（第0帧），攻击状态手动切帧动画
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            if (!fireAttack) {
                // 普通状态：使用NPC.frame绘制当前帧
                spriteBatch.Draw(texture, NPC.Center - screenPos, NPC.frame, drawColor,
                    NPC.rotation, NPC.frame.Size() / 2, NPC.scale, effects, 0f);
            }
            else {
                // 攻击状态：手动计算攻击帧矩形
                int frameHeight = texture.Height / HeadFrameCount;
                int frameY = frameHeight * attackFrame;
                Rectangle sourceRect = new Rectangle(0, frameY, texture.Width, frameHeight);
                Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
                spriteBatch.Draw(texture, NPC.Center - screenPos, sourceRect, drawColor,
                    NPC.rotation, origin, NPC.scale, effects, 0f);
            }

            return false;
        }

        #endregion
    }
}

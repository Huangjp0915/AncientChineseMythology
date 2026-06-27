using Microsoft.Xna.Framework;
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

        /// <summary>
        /// V2 签名时刻的全屏霜冻扭曲（GenericWarp · frost 主题）。喂 Main.screenTarget 的昂贵后处理,
        /// 受单一全屏后处理名额约束(<see cref="ACMShaders.RequestFullscreenSlot"/>): 仅绝对零度/破境时拉满,
        /// 平时强度 0 直接早退。所有氛围/泛光/地纹由 <see cref="AoyuanFrostScreenSystem"/> 单独承担。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || frostWarp <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(frostWarp, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.85f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.1f);
            fx.Parameters["uChroma"]?.SetValue(0.5f);
            fx.Parameters["uRadialPull"]?.SetValue(0.35f); // 轻微向心吸入 = 冻结收口
            fx.Parameters["uMode"]?.SetValue(1f);          // 1 = frost 主题
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 0.6f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion
    }
}

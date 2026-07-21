using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·裂地波 (V3) — 纯视觉地面裂纹动画 (damage=0), 由挥击/震地/入场砸落在脚下生成。
    /// 贴图 874×328×6 帧; 按 32px 分段贴合地形绘制 (V2 曾按 1px 切 874 段, 每帧 874 次 Draw — 已修复)。
    /// ai[0] = 横向缩放 (0 视为 1, 入场/震地可传更大值)。
    /// </summary>
    public class BlackBear_Proj1 : ModProjectile
    {
        private const int TotalFrames = 6;
        private const int FrameTime = 5;
        private const int SegmentPx = 32;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_Proj1";

        public override void SetDefaults() {
            Projectile.hostile = false;   // 纯视觉: 伤害走 Boss 激活帧接触 / GroundShock
            Projectile.friendly = false;
            Projectile.width = 874;
            Projectile.height = 328;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalFrames * FrameTime;
            Projectile.light = 0.4f;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            int age = TotalFrames * FrameTime - Projectile.timeLeft;
            Projectile.frame = Math.Min(age / FrameTime, TotalFrames - 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            int frameHeight = texture.Height / TotalFrames;
            float scaleX = Projectile.ai[0] > 0.01f ? Projectile.ai[0] : 1f;

            int segments = Projectile.width / SegmentPx;
            float drawnSegW = SegmentPx * scaleX;
            float left = Projectile.Center.X - segments * drawnSegW * 0.5f;

            for (int i = 0; i < segments; i++) {
                float worldX = left + (i + 0.5f) * drawnSegW;
                int tileX = (int)MathHelper.Clamp(worldX / 16f, 1, Main.maxTilesX - 2);
                int tileY = (int)MathHelper.Clamp(Projectile.Center.Y / 16f - 3, 1, Main.maxTilesY - 2);

                // 向下找可站立固体面, 让裂纹贴合地形起伏
                while (tileY < Main.maxTilesY - 1) {
                    Tile t = Main.tile[tileX, tileY];
                    if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                        break;
                    tileY++;
                }

                float groundY = tileY * 16f;
                Vector2 drawPos = new Vector2(worldX, groundY) - Main.screenPosition;
                Rectangle src = new(i * SegmentPx, Projectile.frame * frameHeight, SegmentPx, frameHeight);
                Vector2 origin = new(SegmentPx / 2f, frameHeight); // 底边锚地

                Main.EntitySpriteDraw(texture, drawPos, src, lightColor, 0f, origin,
                    new Vector2(scaleX, 1f), SpriteEffects.None, 0);
            }

            return false;
        }
    }
}

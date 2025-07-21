using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    public class BlackBear_Proj1 : ModProjectile
    {
        private int frameCounter = 0;
        private int frameSpeed = 5; // 每帧持续时间
        private int totalFrames = 6; // 总帧数
        private bool initialized = false; // 是否已初始化

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_Proj1"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Projectile.hostile = true; // 敌方伤害
            Projectile.width = 874; // 弹幕宽度
            Projectile.height = 328; // 弹幕高度
            Projectile.friendly = false; // 友方弹幕
            Projectile.tileCollide = false; // 不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Default; // 伤害类型
            Projectile.penetrate = 1; // 穿透
            Projectile.ignoreWater = true; // 无视液体
            Projectile.timeLeft = 120; // 存在时间，单位为帧
            Projectile.alpha = 1; // 透明度
            Projectile.light = 0.5f; // 发光亮度
        }

        public override void AI() {
            if (!initialized) {
                // 初始化时检测与下方物块的距离
                int startX = (int)(Projectile.position.X / 16f);
                int endX = (int)((Projectile.position.X + Projectile.width) / 16f);
                int minY = int.MaxValue;

                for (int x = startX; x <= endX; x++) {
                    int tileY = (int)((Projectile.position.Y + Projectile.height) / 16f);
                    while (tileY < Main.maxTilesY && Main.tile[x, tileY] != null && !Main.tile[x, tileY].HasTile) {
                        tileY++;
                    }
                    minY = Math.Min(minY, tileY * 16);
                }

                // 将弹幕位置设置在物块上方
                Projectile.position.Y = minY - Projectile.height;
                initialized = true;
            }

            frameCounter++;
            if (frameCounter >= frameSpeed) {
                frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= totalFrames) {
                    //Projectile.frame = 0;
                    Projectile.Kill();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            int frameHeight = texture.Height / totalFrames;
            int segmentWidth = texture.Width / 874; // 将图片切割成10份

            for (int i = 0; i < 874; i++) {
                int segmentX = (int)(Projectile.position.X + i * segmentWidth);
                int tileX = segmentX / 16;
                int tileY = (int)((Projectile.position.Y + Projectile.height) / 16f);

                // 只与可以阻挡玩家的物块进行限制
                while (tileY < Main.maxTilesY && Main.tile[tileX, tileY] != null && (!Main.tile[tileX, tileY].HasTile || !Main.tileSolid[Main.tile[tileX, tileY].TileType])) {
                    tileY++;
                }

                int heightAdjustment = tileY * 16 - (int)Projectile.position.Y - Projectile.height;
                Vector2 drawPos = new Vector2(segmentX, Projectile.position.Y + heightAdjustment) - Main.screenPosition;

                Rectangle sourceRectangle = new Rectangle(i * segmentWidth, Projectile.frame * frameHeight, segmentWidth, frameHeight);
                Vector2 origin = new Vector2(segmentWidth / 2f, frameHeight / 2f);

                Main.EntitySpriteDraw(texture, drawPos + new Vector2(0, Projectile.height / 2), sourceRectangle, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            return false;
        }

    }
}

using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精 Attack_3 "立地震地"释放的地面冲击波 (V2 新增)。沿地面横向碾压, 贴地致命, <b>玩家跳跃可躲</b>;
    /// Boss 蓄力期站立不动给远程窗口, 此弹是其收尾威胁。ai[0]=方向(±1)。
    /// 纹理安全: 锚定同目录已存在的 BlackBear_Head_Boss; 实际外观以贴地裂纹 dust + 柔光带绘制。
    /// </summary>
    public class BlackBearGroundShock : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.width = 50;
            Projectile.height = 46;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 55;
            Projectile.light = 0.4f;
        }

        // 找正下方/附近地面顶部 Y, 让冲击波贴地推进
        private float GroundY() {
            int tileX = (int)MathHelper.Clamp(Projectile.Center.X / 16f, 0, Main.maxTilesX - 1);
            int startY = (int)MathHelper.Clamp(Projectile.Center.Y / 16f - 4, 0, Main.maxTilesY - 1);
            for (int y = startY; y < Main.maxTilesY; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return Projectile.Center.Y;
        }

        public override void AI() {
            // 贴地: 中心 Y 平滑对齐到地面顶上方半个身位 (跟随地形起伏)
            float gy = GroundY();
            float targetCenterY = gy - Projectile.height / 2f;
            Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, targetCenterY, 0.4f));
            Projectile.velocity.Y = 0f;

            // 贴地裂纹 dust (上扬)
            if (!Main.dedServ) {
                for (int i = 0; i < 2; i++) {
                    Vector2 p = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-20f, 20f), gy - 4f);
                    Dust d = Dust.NewDustPerfect(p, DustID.Torch, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 6f)), 100, Color.OrangeRed, 1.3f);
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    Dust dirt = Dust.NewDustPerfect(new Vector2(Projectile.Center.X, gy - 2f), DustID.Dirt, new Vector2(Projectile.velocity.X * 0.2f, -Main.rand.NextFloat(2f, 4f)));
                    dirt.scale = 1.1f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (soft == null)
                return false;
            float life = Projectile.timeLeft / 55f;
            float gy = GroundY();
            Vector2 basePos = new Vector2(Projectile.Center.X, gy - 8f) - Main.screenPosition;
            Vector2 origin = soft.Size() / 2f;
            Color glow = TelegraphColors.Lethal * (0.5f * life);
            glow.A = 0;
            // 横向扁光带, 贴地裂纹
            Main.spriteBatch.Draw(soft, basePos, null, glow, 0f, origin, new Vector2(0.9f, 0.28f), SpriteEffects.None, 0f);
            Color core = TelegraphColors.Flame * (0.5f * life);
            core.A = 0;
            Main.spriteBatch.Draw(soft, basePos, null, core, 0f, origin, new Vector2(0.5f, 0.18f), SpriteEffects.None, 0f);
            return false;
        }
    }
}

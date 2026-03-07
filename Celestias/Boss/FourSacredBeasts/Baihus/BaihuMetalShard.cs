using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎金属碎片 — 使用EmberShards纹理采样的高速旋转金属碎片
    /// 快速自旋 + 金属残影尾迹，碎片在旧位置绘制渐隐的银白色残像
    /// 渲染技术：EmberShards子区域随机采样 + 旋转残影 + Sparkle命中闪光
    /// </summary>
    public class BaihuMetalShard : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private int shardVariant;
        private float spinSpeed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 初始化：随机选择碎片变体和旋转速度
            if (Projectile.localAI[0] == 0) {
                shardVariant = Main.rand.Next(9);
                spinSpeed = Main.rand.NextFloat(0.15f, 0.35f) * (Main.rand.NextBool() ? 1 : -1);
                Projectile.localAI[0] = 1;
            }

            Projectile.rotation += spinSpeed;

            // 金属火花微粒
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.Silver,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    120, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.25f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // EmberShards是3x3排列的9个碎片形状，每格约170x170
            Texture2D shardTex = ACMAsset.EmberShards;
            int gridSize = shardTex.Width / 3;
            int gridX = shardVariant % 3;
            int gridY = shardVariant / 3;
            Rectangle shardFrame = new Rectangle(gridX * gridSize, gridY * gridSize, gridSize, gridSize);
            Vector2 shardOrigin = new Vector2(gridSize / 2f, gridSize / 2f);

            // 残影尾迹（AlphaBlend模式下直接绘制）
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = (float)i / trailLen;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];
                float alpha = 0.4f * (1f - progress);
                float scale = 0.13f * (1f - progress * 0.3f);

                Color trailColor = Color.Lerp(Color.White, new Color(180, 180, 200), progress) * alpha;
                sb.Draw(shardTex, trailPos, shardFrame, trailColor, trailRot,
                    shardOrigin, scale, SpriteEffects.None, 0f);
            }

            // 主体碎片
            Color mainColor = new Color(230, 230, 245) * 0.9f;
            sb.Draw(shardTex, drawPos, shardFrame, mainColor, Projectile.rotation,
                shardOrigin, 0.14f, SpriteEffects.None, 0f);

            // Additive层：金属光泽
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            // 高光核心
            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Color metalGlow = new Color(200, 210, 255, 0) * 0.3f;
            sb.Draw(glowTex, drawPos, null, metalGlow, 0f,
                glowOrigin, 0.5f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 碎裂闪光
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Silver, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4),
                    80, default, 1.3f);
                d.noGravity = true;
            }
        }
    }
}

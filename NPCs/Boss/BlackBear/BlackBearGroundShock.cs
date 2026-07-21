using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·地面冲击波 (V3) — 立地震地/黑风怒嚎的贴地岩浪。沿地面横向碾压并跟随地形起伏,
    /// 贴地致命、<b>跳跃可躲</b>; 长蓄力站桩是它的预警。尾段减速淡灭时伤害同步关闭 (与视觉对齐)。
    /// ai[0] = 方向(±1); ai[1] = 计时 (自增)。
    /// </summary>
    public class BlackBearGroundShock : ModProjectile
    {
        private const int LifeTicks = 70;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        private ref float Dir => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.width = 54;
            Projectile.height = 48;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTicks;
            Projectile.light = 0.4f;
        }

        // 找正下方/附近地面顶部 Y, 让冲击波贴地推进
        private float GroundY() {
            int tileX = (int)MathHelper.Clamp(Projectile.Center.X / 16f, 1, Main.maxTilesX - 2);
            int startY = (int)MathHelper.Clamp(Projectile.Center.Y / 16f - 4, 1, Main.maxTilesY - 2);
            for (int y = startY; y < Main.maxTilesY - 1; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return Projectile.Center.Y;
        }

        public override void AI() {
            Timer++;

            // 贴地: 中心 Y 平滑对齐地面上方半身位 (跟随地形起伏)
            float gy = GroundY();
            float targetCenterY = gy - Projectile.height / 2f;
            Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, targetCenterY, 0.4f));
            Projectile.velocity.Y = 0f;

            // 尾段减速消散 (最后 16 帧), 伤害同步关闭
            if (Projectile.timeLeft <= 16) {
                Projectile.velocity.X *= 0.9f;
                if (Projectile.timeLeft <= 8)
                    Projectile.damage = 0;
            }

            // 贴地岩浪 dust: 翻起的土石 + 橙红裂光 (数量 ∝ 速度, 节流)
            if (!Main.dedServ) {
                int n = Projectile.velocity.Length() > 11f ? 2 : 1;
                for (int i = 0; i < n; i++) {
                    Vector2 p = new(Projectile.Center.X + Main.rand.NextFloat(-22f, 22f), gy - 4f);
                    Dust d = Dust.NewDustPerfect(p, DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 6f)), 100, Color.OrangeRed, 1.3f);
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(2)) {
                    Dust dirt = Dust.NewDustPerfect(new Vector2(Projectile.Center.X - Dir * 10f, gy - 2f), DustID.Dirt,
                        new Vector2(-Dir * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(2.5f, 5f)));
                    dirt.scale = 1.2f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (soft == null)
                return false;
            float life = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            float gy = GroundY();
            Vector2 basePos = new Vector2(Projectile.Center.X, gy - 10f) - Main.screenPosition;
            Vector2 origin = soft.Size() / 2f;

            // 横向扁光带 (外红内橙) + 波头亮点
            Color glow = TelegraphColors.Lethal * (0.55f * life);
            glow.A = 0;
            Main.spriteBatch.Draw(soft, basePos, null, glow, 0f, origin, new Vector2(1.0f, 0.30f), SpriteEffects.None, 0f);
            Color core = TelegraphColors.Flame * (0.6f * life);
            core.A = 0;
            Main.spriteBatch.Draw(soft, basePos, null, core, 0f, origin, new Vector2(0.55f, 0.20f), SpriteEffects.None, 0f);
            Color head = new Color(255, 230, 170) * (0.5f * life);
            head.A = 0;
            Main.spriteBatch.Draw(soft, basePos + new Vector2(Dir * 20f, -4f), null, head, 0f, origin, new Vector2(0.22f, 0.14f), SpriteEffects.None, 0f);
            return false;
        }
    }
}

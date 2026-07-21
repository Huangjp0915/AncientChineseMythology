using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·蜜潭 (V3 新增) — 地面场地机制: 琥珀蜜液潭, 踩入不掉血但施加迟缓
    /// (对抗一头冲撞熊时被黏住脚 = 真实威胁, 教学"控制走位地盘")。
    /// 由蜜蜡弹 (Proj2) / 蜜雨 (HoneyDrip) 着地生成; 同屏 ≤6, 超限自动清最旧。
    /// 视觉: 专属 BlackBearHoneyPool.fx 屏幕空间 decal (扰边椭圆 + 焦散流光 + 气泡 + 亮圈)。
    /// ai[0] = 半径 (世界像素, 0 视为 90); ai[1] = 寿命计时 (自增)。
    /// </summary>
    public class BlackBearHoneyPool : ModProjectile
    {
        private const int LifeTicks = 780;   // 13s
        private const int FadeIn = 30;
        private const int FadeOut = 60;
        private const int MaxPools = 6;

        // 专属着色器 (静态缓存一次, 不注册 ACMShaders)
        private static Asset<Effect> _poolFxRef;
        private static Effect PoolFx {
            get {
                if (Main.dedServ)
                    return null;
                _poolFxRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BlackBearHoneyPool", AssetRequestMode.ImmediateLoad);
                return _poolFxRef?.Value;
            }
        }

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        private ref float Radius => ref Projectile.ai[0];
        private ref float Life => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.hostile = false;   // 无伤害: 威胁是迟缓, 不是掉血 (早期 Boss 公平阀门)
            Projectile.friendly = false;
            Projectile.width = 160;
            Projectile.height = 40;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTicks;
            Projectile.light = 0.25f;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            if (Radius < 1f)
                Radius = 90f;
            Projectile.width = (int)(Radius * 2f);
            Projectile.Center = new Vector2(Projectile.Center.X, GroundY() - Projectile.height / 2f + 8f);

            // 同屏蜜潭上限: 杀最旧 (timeLeft 最小)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 0, oldest = -1, oldestLife = int.MaxValue;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI)
                        continue;
                    count++;
                    if (p.timeLeft < oldestLife) { oldestLife = p.timeLeft; oldest = i; }
                }
                if (count >= MaxPools && oldest >= 0)
                    Main.projectile[oldest].Kill();
            }
        }

        private float GroundY() {
            int tileX = (int)MathHelper.Clamp(Projectile.Center.X / 16f, 1, Main.maxTilesX - 2);
            int startY = (int)MathHelper.Clamp(Projectile.Center.Y / 16f - 2, 1, Main.maxTilesY - 2);
            for (int y = startY; y < Main.maxTilesY - 1; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return Projectile.Center.Y;
        }

        /// <summary>寿命包络 0~1 (淡入 → 常驻 → 淡出)。</summary>
        private float Envelope() {
            float t = MathHelper.Clamp(Life / FadeIn, 0f, 1f);
            float o = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOut, 0f, 1f);
            return t * o;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            float env = Envelope();

            // 踩入迟缓 (服务器判定, buff 自动同步)
            if (Main.netMode != NetmodeID.MultiplayerClient && env > 0.5f) {
                Rectangle zone = new((int)(Projectile.Center.X - Radius), (int)(Projectile.Center.Y - 24),
                    (int)(Radius * 2f), 48);
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && player.Hitbox.Intersects(zone))
                        player.AddBuff(BuffID.Slow, 12);
                }
            }

            // 偶发气泡 dust (客户端装饰, 节流)
            if (!Main.dedServ && env > 0.4f && Main.rand.NextBool(9)) {
                Vector2 p = Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius * 0.8f, Radius * 0.8f), Main.rand.NextFloat(-6f, 2f));
                Dust d = Dust.NewDustPerfect(p, DustID.Honey, new Vector2(0, -Main.rand.NextFloat(0.4f, 1.1f)), 100, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope();
            if (env <= 0.02f)
                return false;

            Effect fx = PoolFx;
            if (fx == null) {
                // 着色器缺失兜底: 双层扁椭圆柔光
                Texture2D soft = ACMAsset.SoftGlow;
                if (soft == null)
                    return false;
                Vector2 origin = soft.Size() / 2f;
                Vector2 pos = Projectile.Center - Main.screenPosition;
                Color amber = new Color(220, 140, 30) * (0.5f * env); amber.A = 0;
                Main.spriteBatch.Draw(soft, pos, null, amber, 0f, origin, new Vector2(Radius / 28f, 0.5f), SpriteEffects.None, 0f);
                return false;
            }

            ACMShaders.WorldDecalParams(Projectile.Center, Radius, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(env);
            fx.Parameters["uFlatten"]?.SetValue(0.24f);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
            return false;
        }

        // 生命周期: 卸载时释放着色器引用
        public override void Unload() => _poolFxRef = null;
    }
}

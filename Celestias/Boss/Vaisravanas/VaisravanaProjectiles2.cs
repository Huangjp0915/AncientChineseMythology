using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    #region 仙气地波（随地形起伏）

    /// <summary>
    /// 仙气地波 - 沿地面横向推进，吸附地表/平台高度，迫使纵向跳跃走位。
    /// 区别于观察者的平面扩散环：本波体贴地，遇平台/坡地会随之升降。
    /// </summary>
    internal class ImmortalGroundShock : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Traveled => ref Projectile.localAI[0];
        private ref float Age => ref Projectile.localAI[1];

        private const float MaxTravel = 2400f;
        private const int GraceTicks = 14;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 112;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
        }

        public override bool? CanDamage() => Age > GraceTicks;

        public override void AI() {
            Age++;

            // 横向推进
            Projectile.position.X += Projectile.velocity.X;
            Traveled += MathF.Abs(Projectile.velocity.X);

            // 吸附地表：扫描中心 X 下方第一格实体/平台
            float surfaceY = FindSurfaceY(Projectile.Center.X, Projectile.Center.Y);
            float targetTop = surfaceY - Projectile.height;
            Projectile.position.Y = MathHelper.Lerp(Projectile.position.Y, targetTop, 0.25f);

            Projectile.rotation = 0f;

            if (Traveled > MaxTravel)
                Projectile.Kill();

            if (!VaultUtils.isServer) {
                Vector2 baseP = new Vector2(Projectile.Center.X, surfaceY - 8);
                for (int i = 0; i < 3; i++) {
                    Vector2 p = baseP + new Vector2(Main.rand.NextFloat(-20, 20), -Main.rand.NextFloat(0, Projectile.height));
                    int dust = Dust.NewDust(p, 0, 0, DustID.GoldFlame, 0, -2f, 80, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(Projectile.velocity.X * 0.1f, -Main.rand.NextFloat(1f, 3f));
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.6f) * 0.8f);
        }

        private static float FindSurfaceY(float worldX, float aroundY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)((aroundY - 260f) / 16f);
            for (int ty = startTileY; ty < startTileY + 70; ty++) {
                if (tileX < 0 || tileX >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY)
                    continue;
                Tile t = Main.tile[tileX, ty];
                if (t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]))
                    return ty * 16f;
            }
            return aroundY;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SlashBurst;
            if (tex == null) return false;

            SpriteBatch sb = Main.spriteBatch;
            float alpha = MathHelper.Clamp(Age / (float)GraceTicks, 0.25f, 1f);
            if (Projectile.timeLeft < 30) alpha *= Projectile.timeLeft / 30f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 bottom = new Vector2(Projectile.Center.X, Projectile.position.Y + Projectile.height) - Main.screenPosition;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height); // 底部中心
            float pulse = 1f + MathF.Sin(Age * 0.4f) * 0.08f;
            Vector2 scale = new Vector2(Projectile.width / (float)tex.Width * 1.6f, Projectile.height / (float)tex.Height * pulse);

            Color outer = VaisravanaHelper.TowerGold * (alpha * 0.6f); outer.A = 0;
            sb.Draw(tex, bottom, null, outer, 0f, origin, scale * new Vector2(1.5f, 1f), SpriteEffects.None, 0f);
            Color core = VaisravanaHelper.PureWhite * (alpha * 0.7f); core.A = 0;
            sb.Draw(tex, bottom, null, core, 0f, origin, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    #endregion

    #region 库藏封印·金环（向内收缩，带安全道）

    /// <summary>
    /// 库藏封印金环 - 从大半径向内收缩的金环，留出一条标记安全道。
    /// 玩家须在收缩前进入安全道扇区。
    /// </summary>
    internal class TreasurySealRing : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float SafeAngle => ref Projectile.ai[0];
        private ref float StartRadius => ref Projectile.ai[1];
        private ref float Radius => ref Projectile.localAI[0];
        private ref float Age => ref Projectile.localAI[1];

        private const int Telegraph = 22;
        private const float ContractSpeed = 5.2f;
        private const float SafeHalfWidth = 0.66f; // ≈38°
        private const float Band = 36f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 400;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Age > Telegraph;

        public override void AI() {
            if (Age == 0)
                Radius = StartRadius <= 0 ? 760f : StartRadius;
            Age++;

            if (Age > Telegraph) {
                Radius -= ContractSpeed;
                if (Radius < 38f)
                    Projectile.Kill();
            }

            if (!VaultUtils.isServer && Age % 2 == 0) {
                // 安全道两侧高亮提示
                for (int s = -1; s <= 1; s += 2) {
                    float a = SafeAngle + s * SafeHalfWidth;
                    Vector2 p = Projectile.Center + a.ToRotationVector2() * Radius;
                    int dust = Dust.NewDust(p, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.55f) * 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 tc = targetHitbox.Center.ToVector2();
            Vector2 toTarget = tc - Projectile.Center;
            float dist = toTarget.Length();
            if (dist < Radius - Band || dist > Radius + Band)
                return false;

            float angle = toTarget.ToRotation();
            if (MathF.Abs(MathHelper.WrapAngle(angle - SafeAngle)) < SafeHalfWidth)
                return false; // 安全道

            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D dot = ACMAsset.LightShot;
            if (dot == null) return false;

            float alpha = Age < Telegraph ? (0.35f + 0.4f * (Age / (float)Telegraph)) : 0.9f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            int points = 64;
            Vector2 origin = dot.Size() / 2f;
            for (int i = 0; i < points; i++) {
                float angle = MathHelper.TwoPi * i / points;
                bool safe = MathF.Abs(MathHelper.WrapAngle(angle - SafeAngle)) < SafeHalfWidth;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Radius - Main.screenPosition;
                // 安全缝=翠玉(可穿越), 危险段=琉璃金(收缩压制) —— 与全局预警色语言一致, 金=财气危险
                Color c = (safe ? TelegraphColors.Safe : TelegraphColors.Gold) * alpha * (safe ? 0.4f : 0.9f);
                c.A = 0;
                sb.Draw(dot, pos, null, c, 0f, origin, safe ? 0.3f : 0.46f, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    #endregion

    #region 夜叉镜弹（仅反射角可躲）

    /// <summary>
    /// 夜叉镜弹 - 沿镜轴对称会聚的镜弹，唯有站在反射轴线上方能安全穿越。
    /// </summary>
    internal class YakshaMirrorBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.6f) * 0.7f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ACMAsset.LightShot;
            if (tex == null) return false;
            Vector2 origin = tex.Size() / 2f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Color tc = VaisravanaHelper.TowerGold * (0.5f * fade); tc.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(tex, pos, null, tc, Projectile.oldRot[i], origin, new Vector2(0.5f * fade, 0.18f), SpriteEffects.None, 0f);
            }

            Color core = VaisravanaHelper.PureWhite; core.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, core, Projectile.rotation, origin, new Vector2(0.55f, 0.3f), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    #endregion

    #region 库藏封印符（终极宝塔地纹预告）

    /// <summary>
    /// 库藏封印符 - 终极宝塔激光落点的地面符文预告（无伤害纯演出）。
    /// </summary>
    internal class TreasurySealRune : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        private const int Duration = 78;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Duration;
        }

        public override bool? CanDamage() => false;
        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            // 把符文铺设到激光朝向击中的地面处
            NPC owner = Main.npc[(int)OwnerIndex];
            Vector2 start = owner.active ? owner.Center : Projectile.Center;
            Vector2 dir = LaserAngle.ToRotationVector2();
            Vector2 hit = start + dir * 1400f;
            for (int step = 1; step <= 130; step++) {
                Vector2 p = start + dir * (step * 16f);
                int tx = (int)(p.X / 16f), ty = (int)(p.Y / 16f);
                if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY)
                    break;
                Tile t = Main.tile[tx, ty];
                if (t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])) {
                    hit = p;
                    break;
                }
            }
            Projectile.Center = hit;
        }

        public override void AI() {
            Age++;
            if (!VaultUtils.isServer) {
                float r = 40f + MathF.Sin(Age * 0.2f) * 6f;
                for (int i = 0; i < 2; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 p = Projectile.Center + a.ToRotationVector2() * r;
                    int dust = Dust.NewDust(p, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.5f) * 0.7f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D star = ACMAsset.BlankStar;
            Texture2D glow = ACMAsset.SoftGlow;
            if (star == null) return false;

            float progress = Age / Duration;
            float alpha = MathHelper.Clamp(progress < 0.85f ? 0.3f + progress * 0.7f : (1f - progress) / 0.15f, 0f, 1f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (glow != null) {
                Color g = VaisravanaHelper.TowerGold * (alpha * 0.55f); g.A = 0;
                sb.Draw(glow, drawPos, null, g, 0f, glow.Size() / 2f, 1.1f, SpriteEffects.None, 0f);
            }

            int spokes = 8;
            Vector2 origin = star.Size() / 2f;
            for (int i = 0; i < spokes; i++) {
                float a = MathHelper.TwoPi * i / spokes + Age * 0.05f;
                Vector2 p = drawPos + a.ToRotationVector2() * 36f;
                Color c = VaisravanaHelper.PureWhite * (alpha * 0.6f); c.A = 0;
                sb.Draw(star, p, null, c, a, origin, 0.3f, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    #endregion
}

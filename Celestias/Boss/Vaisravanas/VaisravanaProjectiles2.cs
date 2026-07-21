using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
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

        /// <summary>ai0>0 时作为射程覆盖（天王步落地短波用），否则默认全程 2400px。</summary>
        private float MaxTravel => Projectile.ai[0] > 0f ? Projectile.ai[0] : 2400f;
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

    #region 金刚破军矛（一击必杀式直线金矛）

    /// <summary>
    /// 金刚破军矛 - 金刚怒目式蓄力后的一击。extraUpdates=3 下实际 170px/f，
    /// 路径在蓄力期已由 DrawBeam 完整预告（锁死后不再追踪），生成即闪电贯穿。
    /// </summary>
    internal class VajraSpear : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 24;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
            Projectile.extraUpdates = 3;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 音爆尾流：速度门控的拉伸金尘（只在高速时存在，卖速度）
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.GoldFlame, -Projectile.velocity * 0.06f, 80, default, 1.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.6f) * 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 矛体：过曝金带（尾迹方向拉长）+ 白热矛尖
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tail = Projectile.Center - dir * 320f;
            Color core = TelegraphColors.Holy; core.A = 255;
            Color edge = TelegraphColors.Gold; edge.A = 130;
            ACMShaders.DrawBeam(tail, Projectile.Center + dir * 60f, 18f, core, edge, 1f,
                flowSpeed: 3.2f, flowScale: 1.6f, coreSharp: 2.6f, coreGlow: 1.1f);

            if (ACMAsset.LightShot != null) {
                SpriteBatch sb = Main.spriteBatch;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color tip = VaisravanaHelper.DivineWhite; tip.A = 0;
                sb.Draw(ACMAsset.LightShot, drawPos, null, tip, Projectile.rotation,
                    ACMAsset.LightShot.Size() / 2f, new Vector2(1.6f, 0.5f), SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 1.1f }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(7f, 7f), 80, default, 1.9f);
                d.noGravity = true;
            }
        }
    }

    #endregion

    #region 镇压天光柱（天光垂落 / 塔光柱镇压）

    /// <summary>
    /// 镇压天光柱 - 从天而降的金光镇压柱，经 VaisravanaPillarBrand 着色器绘制。
    /// ai0=模式: 0=天光垂落(静止, ai1=起手延迟, 40f 细线预告→26f 爆发);
    ///           1=塔光柱镇压(ai1=收拢方向 ±1, 45f 预告→90f 爆发并以 0.55px/f 夹击, 总收拢上限保证中缝 ≥260px)。
    /// 伤害窗口与爆发视觉严格对齐（预告期零伤害）。
    /// </summary>
    internal class VaisravanaLightPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Mode => ref Projectile.ai[0];
        private ref float Param => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];
        private ref float Drifted => ref Projectile.localAI[1];

        private bool IsSuppress => Mode > 0.5f;
        private int Delay => IsSuppress ? 0 : (int)Param;
        private int TelegraphTime => IsSuppress ? 45 : 40;
        private int BurstTime => IsSuppress ? 90 : 26;
        private const int FadeTime = 12;
        private float PillarWidth => IsSuppress ? 130f : 110f;
        /// <summary>收拢上限：保证双柱内缘中缝始终 ≥260px（2·(480-280)-130=270）。</summary>
        private const float MaxDrift = 280f;

        private float pillarTopY;
        private float pillarBottomY;
        private bool anchored;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2200;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override bool ShouldUpdatePosition() => false;

        private int BurstStart => Delay + TelegraphTime;
        private int BurstEnd => BurstStart + BurstTime;

        // 爆发展开 4f 后才开始判伤：伤害窗口严格贴合"全宽金柱"视觉
        public override bool? CanDamage() => Age > BurstStart + 4 && Age <= BurstEnd;

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            Projectile.timeLeft = BurstEnd + FadeTime;
            AnchorToGround();
        }

        /// <summary>吸附地面：柱底落在生成点下方第一处实体面，柱顶伸入高空。</summary>
        private void AnchorToGround() {
            float surfaceY = Projectile.Center.Y + 400f;
            int tileX = (int)(Projectile.Center.X / 16f);
            int startTileY = (int)((Projectile.Center.Y - 60f) / 16f);
            for (int ty = startTileY; ty < startTileY + 90; ty++) {
                if (tileX < 0 || tileX >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY)
                    continue;
                Tile t = Main.tile[tileX, ty];
                if (t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])) {
                    surfaceY = ty * 16f;
                    break;
                }
            }
            pillarBottomY = surfaceY;
            pillarTopY = surfaceY - (IsSuppress ? 1080f : 920f);
            anchored = true;
        }

        public override void AI() {
            Age++;
            if (!anchored)
                AnchorToGround();

            // 镇压模式：爆发期横向收拢夹击（限幅保证中缝）
            if (IsSuppress && Age > BurstStart && Age <= BurstEnd && Drifted < MaxDrift) {
                float drift = 0.55f;
                Projectile.position.X += MathF.Sign(Param) * drift;
                Drifted += drift;
            }

            // 爆发首帧：镇压音 + 底座金尘迸射
            if ((int)Age == BurstStart + 1) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = IsSuppress ? -0.35f : 0.1f, Volume = 1.1f },
                    new Vector2(Projectile.Center.X, pillarBottomY));
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(IsSuppress ? 6f : 4f);
                    for (int i = 0; i < 12; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(2f, 7f));
                        Dust d = Dust.NewDustPerfect(new Vector2(
                            Projectile.Center.X + Main.rand.NextFloat(-PillarWidth, PillarWidth) * 0.4f, pillarBottomY - 4f),
                            DustID.GoldFlame, vel, 90, default, 1.8f);
                        d.noGravity = true;
                    }
                }
            }

            // 爆发期间基座细尘 + 光照
            if (Age > BurstStart && Age <= BurstEnd) {
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(new Vector2(
                        Projectile.Center.X + Main.rand.NextFloat(-PillarWidth, PillarWidth) * 0.35f,
                        pillarBottomY - Main.rand.NextFloat(0f, 60f)),
                        DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 110, default, 1.3f);
                    d.noGravity = true;
                }
                for (int i = 0; i < 4; i++) {
                    Vector2 lightPos = new(Projectile.Center.X, pillarBottomY - 120f - i * 220f);
                    Lighting.AddLight(lightPos, new Vector3(1f, 0.9f, 0.6f) * 1.1f);
                }
            }
            else if (Age <= BurstStart && Age > Delay) {
                Lighting.AddLight(new Vector2(Projectile.Center.X, pillarBottomY - 60f), new Vector3(0.8f, 0.7f, 0.4f) * 0.5f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 柱体矩形判定（仅爆发期，见 CanDamage）
            float halfW = PillarWidth * 0.42f; // 伤害盒略窄于视觉宽度（宽仁判定）
            Rectangle pillarRect = new(
                (int)(Projectile.Center.X - halfW), (int)pillarTopY,
                (int)(halfW * 2f), (int)(pillarBottomY - pillarTopY));
            return pillarRect.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Age <= Delay)
                return false;

            float telegraph;
            float intensity;
            if (Age <= BurstStart) {
                // 预告态：细线渐亮
                float t = (Age - Delay) / (float)TelegraphTime;
                telegraph = 1f;
                intensity = 0.35f + t * 0.5f;
            }
            else if (Age <= BurstEnd) {
                // 爆发态：6f 内从细线撑开到全宽
                float open = MathHelper.Clamp((Age - BurstStart) / 6f, 0f, 1f);
                telegraph = 1f - open;
                intensity = 1f;
            }
            else {
                // 收尾淡出
                float fade = 1f - MathHelper.Clamp((Age - BurstEnd) / FadeTime, 0f, 1f);
                telegraph = 0f;
                intensity = fade;
            }

            VaisravanaHelper.DrawPillarBrand(
                new Vector2(Projectile.Center.X, pillarTopY), pillarBottomY - pillarTopY, PillarWidth,
                intensity, telegraph, Projectile.whoAmI * 0.173f,
                TelegraphColors.Holy, TelegraphColors.Gold, IsSuppress ? 0.85f : 1.25f);

            // 镇压模式：柱顶悬浮宝塔（"托塔镇压"的具象来源）
            if (IsSuppress) {
                Texture2D towerTex = VaisravanaHelper.TowerTexture;
                if (towerTex != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    float bob = MathF.Sin(Age * 0.06f + Projectile.whoAmI) * 6f;
                    Vector2 capPos = new Vector2(Projectile.Center.X, pillarTopY + 30f + bob) - Main.screenPosition;
                    float capScale = 1.15f + intensity * 0.2f;
                    if (ACMAsset.SoftGlow != null) {
                        Color glow = VaisravanaHelper.TowerGold * (0.55f * intensity); glow.A = 0;
                        sb.Draw(ACMAsset.SoftGlow, capPos, null, glow, 0f, ACMAsset.SoftGlow.Size() / 2f, 1.3f * capScale, SpriteEffects.None, 0f);
                    }
                    Color aura = TelegraphColors.Gold * (0.6f * intensity); aura.A = 0;
                    sb.Draw(towerTex, capPos, null, aura, 0f, towerTex.Size() / 2f, capScale * 1.14f, SpriteEffects.None, 0f);
                    sb.Draw(towerTex, capPos, null, Color.White * intensity, 0f, towerTex.Size() / 2f, capScale, SpriteEffects.None, 0f);
                }
            }
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

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    #region 1. 闪电节点 - 雷链穿刺核心弹幕（ElectricArcSheet帧动画 + 顶点电弧）

    /// <summary>
    /// 闪电节点 - 固定在空中，延迟后与相邻节点间产生电弧伤害
    /// ai[0] = 节点编号, ai[1] = 总节点数
    /// 绘制：节点用BlankStar旋转星光 + ElectricArcSheet帧动画装饰
    /// 连线用ColoredVertex TriangleStrip + LightningBranch纹理绘制锯齿电弧
    /// </summary>
    public class AoshunLightningNode : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float nodePhase;
        private bool activated;
        private float trailOffset;
        private const int ActivationDelay = 40;
        private const int Duration = 180;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ActivationDelay + Duration;
        }

        public override void AI() {
            nodePhase += 0.08f;
            trailOffset += 0.04f;
            Projectile.velocity = Vector2.Zero;
            int timer = (ActivationDelay + Duration) - Projectile.timeLeft;

            if (timer < ActivationDelay) {
                if (!VaultUtils.isServer && timer % 6 == 0) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15), DustID.Electric);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = Main.rand.NextVector2Circular(1, 1);
                }
            }
            else {
                activated = true;
                if (!VaultUtils.isServer && timer % 8 == 0)
                    FindAndArcToNeighbors();
            }

            float breathe = 0.5f + MathF.Sin(nodePhase * 2f) * 0.3f;
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * breathe);
        }

        private void FindAndArcToNeighbors() {
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == totalNodes) {
                    Vector2 start = Projectile.Center;
                    Vector2 end = p.Center;
                    int steps = (int)(Vector2.Distance(start, end) / 25f);
                    for (int s = 0; s < steps; s++) {
                        float t = (float)s / steps;
                        Vector2 pos = Vector2.Lerp(start, end, t) + Main.rand.NextVector2Circular(8, 8);
                        var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 1.3f;
                        d.velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
                    }
                    break;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!activated) return false;
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            if (Vector2.Distance(Projectile.Center, targetCenter) < 50f)
                return true;
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == totalNodes) {
                    float point = 0f;
                    if (Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(), targetHitbox.Size(),
                        Projectile.Center, p.Center, 20f, ref point))
                        return true;
                    break;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(nodePhase * 3f) * 0.25f;
            float alpha = activated ? 0.8f : (0.3f + MathF.Sin(nodePhase * 5f) * 0.2f);

            // === 激活后：顶点TriangleStrip绘制电弧连线 ===
            if (activated) {
                DrawVertexArcToNeighbor(sb, gd);
            }

            // === 节点本体：BlankStar旋转星光 ===
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex != null) {
                Vector2 starOrigin = starTex.Size() / 2f;
                float starScale = 0.25f * pulse;
                Color starColor = AoshunHelper.LightningBlue * alpha * 0.7f;
                starColor.A = 0;
                sb.Draw(starTex, drawPos, null, starColor, nodePhase * 0.8f, starOrigin, starScale, SpriteEffects.None, 0f);
                // 第二层反转旋转
                Color starColor2 = AoshunHelper.ElectricWhite * alpha * 0.4f;
                starColor2.A = 0;
                sb.Draw(starTex, drawPos, null, starColor2, -nodePhase * 1.2f, starOrigin, starScale * 0.7f, SpriteEffects.None, 0f);
            }

            // === ElectricArcSheet帧动画电弧装饰 ===
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null && activated) {
                int arcIndex = ((int)(nodePhase * 8f)) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new(sourceRect.Width / 2f, sourceRect.Height / 2f);
                float arcAlpha = 0.35f * alpha * (0.6f + 0.4f * MathF.Sin(nodePhase * 6f));
                Color arcColor = AoshunHelper.LightningBlue * arcAlpha;
                arcColor.A = 0;
                sb.Draw(arcSheet, drawPos, sourceRect, arcColor, nodePhase * 1.5f, arcOrigin, 0.15f * pulse, SpriteEffects.None, 0f);
                // 对称一份
                sb.Draw(arcSheet, drawPos, sourceRect, arcColor * 0.6f, nodePhase * 1.5f + MathHelper.Pi, arcOrigin, 0.12f * pulse, SpriteEffects.FlipHorizontally, 0f);
            }

            // === SoftGlow核心光晕 ===
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color outerColor = AoshunHelper.ThunderPurple * 0.3f * alpha * pulse;
                outerColor.A = 0;
                sb.Draw(glowTex, drawPos, null, outerColor, 0f, glowOrigin, 1.0f * pulse, SpriteEffects.None, 0f);
                Color coreColor = AoshunHelper.ElectricWhite * 0.6f * alpha;
                coreColor.A = 0;
                sb.Draw(glowTex, drawPos, null, coreColor, 0f, glowOrigin, 0.4f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        /// <summary>
        /// 用ColoredVertex TriangleStrip + LightningBranch纹理绘制到相邻节点的锯齿电弧
        /// </summary>
        private void DrawVertexArcToNeighbor(SpriteBatch sb, GraphicsDevice gd) {
            int nodeIndex = (int)Projectile.ai[0];
            int totalNodes = (int)Projectile.ai[1];
            int nextIndex = (nodeIndex + 1) % totalNodes;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (!p.active || p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] != nextIndex || (int)p.ai[1] != totalNodes) continue;

                Vector2 start = Projectile.Center - Main.screenPosition;
                Vector2 end = p.Center - Main.screenPosition;
                Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
                Vector2 perp = new(-direction.Y, direction.X);
                float totalDist = Vector2.Distance(start, end);
                int segments = Math.Max((int)(totalDist / 18f), 4);

                // 切换到Additive混合
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                // 构建锯齿电弧顶点条带
                Texture2D branchTex = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                List<ColoredVertex> vertices = new();
                float halfWidth = 18f;
                float flicker = MathF.Sin(nodePhase * 6f + Projectile.whoAmI) * 0.3f + 0.7f;

                for (int s = 0; s <= segments; s++) {
                    float t = (float)s / segments;
                    Vector2 basePoint = Vector2.Lerp(start, end, t);
                    // 锯齿偏移 - 中间最大，两端为0
                    float zigzag = MathF.Sin(t * MathF.PI * 5f + nodePhase * 4f) * 20f * MathF.Sin(t * MathF.PI);
                    basePoint += perp * zigzag;

                    float widthFade = MathF.Sin(t * MathF.PI) * halfWidth * flicker;
                    Color c = Color.Lerp(AoshunHelper.LightningBlue * 0.8f, AoshunHelper.ElectricWhite, t) * flicker;

                    vertices.Add(new ColoredVertex(basePoint + perp * widthFade, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePoint - perp * widthFade, new Vector3(t + trailOffset, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = branchTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                // 第二层更窄更亮的内层电弧
                List<ColoredVertex> innerVerts = new();
                for (int s = 0; s <= segments; s++) {
                    float t = (float)s / segments;
                    Vector2 basePoint = Vector2.Lerp(start, end, t);
                    float zigzag = MathF.Sin(t * MathF.PI * 5f + nodePhase * 4f) * 20f * MathF.Sin(t * MathF.PI);
                    basePoint += perp * zigzag;
                    float widthInner = MathF.Sin(t * MathF.PI) * halfWidth * 0.4f * flicker;
                    Color cInner = AoshunHelper.ElectricWhite * 0.9f * flicker;

                    innerVerts.Add(new ColoredVertex(basePoint + perp * widthInner, new Vector3(t + trailOffset * 1.5f, 0, 1), cInner));
                    innerVerts.Add(new ColoredVertex(basePoint - perp * widthInner, new Vector3(t + trailOffset * 1.5f, 1, 1), cInner));
                }
                if (innerVerts.Count >= 3) {
                    gd.Textures[0] = VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts.ToArray(), 0, innerVerts.Count - 2);
                }

                // 恢复AlphaBlend
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                break;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }

    #endregion

    #region 2. 龙鳞弹幕 - 顶点拖尾 + EmberShards碎片纹理

    /// <summary>
    /// 带电龙鳞 - 从蠕虫身体段抛射，带重力和弹跳
    /// 绘制：ColoredVertex TriangleStrip拖尾 + EmberShards碎片纹理作为弹体
    /// 弹跳时Sparkle闪光
    /// </summary>
    public class AoshunDragonScale : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int bounceCount;
        private float scalePhase;
        private float trailOffset;
        private float bounceFlash;

        // 存储历史位置和旋转用于顶点拖尾
        private readonly List<Vector2> oldPositions = new();
        private readonly List<float> oldRotations = new();
        private const int MaxTrailLength = 12;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            scalePhase += 0.15f;
            trailOffset += 0.06f;
            Projectile.velocity.Y += 0.15f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            // 记录拖尾历史
            if (!VaultUtils.isServer) {
                oldPositions.Insert(0, Projectile.Center);
                oldRotations.Insert(0, Projectile.rotation);
                if (oldPositions.Count > MaxTrailLength) {
                    oldPositions.RemoveAt(oldPositions.Count - 1);
                    oldRotations.RemoveAt(oldRotations.Count - 1);
                }
            }

            // 弹跳闪光衰减
            if (bounceFlash > 0) bounceFlash *= 0.9f;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = -Projectile.velocity * 0.08f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.NorthSeaCyan.ToVector3() * 0.4f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bounceCount++;
            if (bounceCount >= 2) return true;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 1f)
                Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 1f)
                Projectile.velocity.X = -oldVelocity.X * 0.5f;
            bounceFlash = 1f;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(3, 3);
                    d.scale = 1.5f;
                }
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(scalePhase * 2f) * 0.15f;

            // === 顶点TriangleStrip拖尾（Additive混合） ===
            if (oldPositions.Count >= 3) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                List<ColoredVertex> vertices = new();
                int count = oldPositions.Count;
                for (int i = 0; i < count; i++) {
                    float t = (float)i / count;
                    float scaleFactor = 1f - t;
                    Vector2 basePos = oldPositions[i] - Main.screenPosition;

                    // 根据速度方向计算法线
                    Vector2 dir;
                    if (i < count - 1)
                        dir = (oldPositions[i] - oldPositions[Math.Min(i + 1, count - 1)]).SafeNormalize(Vector2.UnitX);
                    else
                        dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perpDir = new(-dir.Y, dir.X);

                    float width = 14f * scaleFactor * pulse;
                    Color c = Color.Lerp(AoshunHelper.NorthSeaCyan * 0.9f, AoshunHelper.ThunderPurple * 0.4f, t);
                    c.A = 0;

                    vertices.Add(new ColoredVertex(basePos + perpDir * width, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePos - perpDir * width, new Vector3(t + trailOffset, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // === EmberShards碎片纹理作为弹体 ===
            Texture2D shardsTex = ACMAsset.EmberShards;
            if (shardsTex != null) {
                // 从9块碎片中取一块（基于whoAmI决定）
                int shardIndex = Projectile.whoAmI % 9;
                int col = shardIndex % 3;
                int row = shardIndex / 3;
                int shardW = shardsTex.Width / 3;
                int shardH = shardsTex.Height / 3;
                Rectangle shardRect = new(col * shardW, row * shardH, shardW, shardH);
                Vector2 shardOrigin = new(shardW / 2f, shardH / 2f);

                Color shardColor = Color.Lerp(AoshunHelper.NorthSeaCyan, AoshunHelper.LightningBlue, 0.5f + MathF.Sin(scalePhase) * 0.3f);
                shardColor *= 0.85f;
                shardColor.A = 30;
                float shardScale = 0.18f * pulse;
                sb.Draw(shardsTex, drawPos, shardRect, shardColor, Projectile.rotation, shardOrigin, shardScale, SpriteEffects.None, 0f);

                // 白色高光层
                Color whiteLayer = AoshunHelper.ElectricWhite * 0.4f;
                whiteLayer.A = 0;
                sb.Draw(shardsTex, drawPos, shardRect, whiteLayer, Projectile.rotation, shardOrigin, shardScale * 0.8f, SpriteEffects.None, 0f);
            }

            // === 弹跳Sparkle闪光 ===
            if (bounceFlash > 0.05f) {
                Texture2D sparkleTex = ACMAsset.Sparkle;
                if (sparkleTex != null) {
                    Vector2 sparkleOrigin = sparkleTex.Size() / 2f;
                    Color flashColor = AoshunHelper.ElectricWhite * bounceFlash * 0.6f;
                    flashColor.A = 0;
                    sb.Draw(sparkleTex, drawPos, null, flashColor, scalePhase * 2f, sparkleOrigin, 0.2f * bounceFlash, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 3. 龙卷风 - 顶点螺旋漏斗 + Smoke多帧烟雾

    /// <summary>
    /// 龙卷风弹幕 - 缓慢追踪玩家，大范围碰撞，带击退
    /// 绘制：多层ColoredVertex TriangleStrip螺旋环构成漏斗形
    ///       Smoke灰度图4x4帧动画叠加体积感
    ///       ElectricArcSheet穿插电弧
    /// </summary>
    public class AoshunTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float spinAngle;
        private float tornadoAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
        }

        public override void AI() {
            spinAngle += 0.2f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.03f);

            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.02f);
                float speed = Math.Min(Projectile.velocity.Length(), 4f);
                if (speed < 2f) speed = 2f;
                Projectile.velocity = newAngle.ToRotationVector2() * speed;
            }

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                float dist = Vector2.Distance(Projectile.Center, p.Center);
                if (dist < 200f && dist > 30f) {
                    Vector2 pull = (Projectile.Center - p.Center).SafeNormalize(Vector2.Zero) * 0.3f;
                    p.velocity += pull;
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                float angle = spinAngle + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 25f + Main.rand.NextFloat(20f);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                dustPos.Y += Main.rand.NextFloat(-50, 0);
                var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool(3) ? DustID.Electric : DustID.Cloud);
                d.noGravity = true;
                d.scale = 1.5f + Main.rand.NextFloat(0.5f);
                d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f + new Vector2(0, -2);
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.StormGray.ToVector3() * 0.5f * tornadoAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < 55f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // === 顶点绘制螺旋漏斗（Additive混合） ===
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D stripTex = ACMAsset.SoftGlow ?? VaultAsset.placeholder2.Value;

            // 绘制6层螺旋环
            for (int layer = 0; layer < 6; layer++) {
                float layerY = -layer * 18f; // 向上叠加
                float layerRadius = 40f - layer * 5f; // 越高越窄
                float layerRot = spinAngle * (1.2f + layer * 0.3f) * (layer % 2 == 0 ? 1 : -1);
                float layerAlpha = tornadoAlpha * (0.45f - layer * 0.05f);

                List<ColoredVertex> ringVerts = new();
                int ringSegments = 24;
                for (int s = 0; s <= ringSegments; s++) {
                    float t = (float)s / ringSegments;
                    float angle = layerRot + MathHelper.TwoPi * t;
                    Vector2 circlePoint = drawPos + angle.ToRotationVector2() * layerRadius + new Vector2(0, layerY);
                    float halfW = 8f - layer * 0.8f;

                    Color c = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.LightningBlue, layer / 5f) * layerAlpha;

                    ringVerts.Add(new ColoredVertex(circlePoint + new Vector2(0, -halfW), new Vector3(t, 0, 1), c));
                    ringVerts.Add(new ColoredVertex(circlePoint + new Vector2(0, halfW), new Vector3(t, 1, 1), c));
                }

                if (ringVerts.Count >= 3) {
                    gd.Textures[0] = stripTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, ringVerts.ToArray(), 0, ringVerts.Count - 2);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // === Smoke帧动画叠加体积感 ===
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex != null) {
                int smokeFrame = ((int)(spinAngle * 3f)) % 16;
                int smokeCol = smokeFrame % 4;
                int smokeRow = smokeFrame / 4;
                int frameW = smokeTex.Width / 4;
                int frameH = smokeTex.Height / 4;
                Rectangle smokeRect = new(smokeCol * frameW, smokeRow * frameH, frameW, frameH);
                Vector2 smokeOrigin = new(frameW / 2f, frameH / 2f);

                // 中心烟雾
                Color smokeColor = AoshunHelper.StormGray * tornadoAlpha * 0.35f;
                smokeColor.A = 80;
                sb.Draw(smokeTex, drawPos + new Vector2(0, -30), smokeRect, smokeColor, spinAngle * 0.3f, smokeOrigin, 0.18f, SpriteEffects.None, 0f);

                // 第二帧偏移
                int smokeFrame2 = (smokeFrame + 7) % 16;
                int sCol2 = smokeFrame2 % 4;
                int sRow2 = smokeFrame2 / 4;
                Rectangle smokeRect2 = new(sCol2 * frameW, sRow2 * frameH, frameW, frameH);
                sb.Draw(smokeTex, drawPos + new Vector2(0, -60), smokeRect2, smokeColor * 0.7f, -spinAngle * 0.4f, smokeOrigin, 0.14f, SpriteEffects.FlipHorizontally, 0f);
            }

            // === 电弧穿插 ===
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null && tornadoAlpha > 0.5f) {
                int arcIdx = ((int)(spinAngle * 6f)) % 4;
                int arcH = arcSheet.Height / 4;
                Rectangle arcRect = new(0, arcIdx * arcH, arcSheet.Width, arcH);
                Vector2 arcOrigin = new(arcRect.Width / 2f, arcRect.Height / 2f);
                Color arcColor = AoshunHelper.LightningBlue * 0.25f * tornadoAlpha;
                arcColor.A = 0;
                sb.Draw(arcSheet, drawPos + new Vector2(0, -40), arcRect, arcColor, spinAngle * 2f, arcOrigin, 0.12f, SpriteEffects.None, 0f);
            }

            // === 底部涡流SoftGlow ===
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color vortexColor = AoshunHelper.ThunderPurple * 0.3f * tornadoAlpha;
                vortexColor.A = 0;
                sb.Draw(glowTex, drawPos, null, vortexColor, spinAngle, glowOrigin, 0.9f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 5f;
                d.scale = 2f;
            }
        }
    }

    #endregion

    #region 4. 天雷印 - LightningBranch预警 + SlashBurst引爆 + 顶点雷柱

    /// <summary>
    /// 天雷印标记 - 固定在地面/空中，倒计时后引爆为巨大雷击柱
    /// 绘制：预警阶段用LightningBranch灰度图绘制旋转符文圈
    ///       引爆时SlashBurst放射闪光 + 顶点TriangleStrip绘制雷柱
    /// </summary>
    public class AoshunThunderSeal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float sealPhase;
        private bool detonated;
        private Vector2 detonationCenter; // 引爆中心（引爆前记录）

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            sealPhase += 0.1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0]--;

            if (Projectile.ai[0] > 0) {
                float urgency = Math.Clamp(1f - Projectile.ai[0] / 90f, 0f, 1f);
                if (!VaultUtils.isServer) {
                    int particleCount = (int)(urgency * 4) + 1;
                    for (int i = 0; i < particleCount; i++) {
                        Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(30 + urgency * 20, 30 + urgency * 20);
                        var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                        d.noGravity = true;
                        d.scale = 1f + urgency;
                        d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + urgency * 3f);
                    }
                }
                if (Projectile.ai[0] == 10)
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
                Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.4f * urgency);
            }
            else if (!detonated) {
                detonated = true;
                detonationCenter = Projectile.Center;
                Projectile.hostile = true;
                Projectile.width = 80;
                Projectile.height = 800;
                Projectile.position.X -= 20;
                Projectile.position.Y -= 700;
                Projectile.timeLeft = 15;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.5f }, Projectile.Center);

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 20; i++) {
                        Vector2 dustPos = detonationCenter + new Vector2(Main.rand.NextFloat(-40, 40), Main.rand.NextFloat(-300, 0));
                        var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 2.5f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-8, -2));
                    }
                }
                Lighting.AddLight(detonationCenter, AoshunHelper.ElectricWhite.ToVector3() * 2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (!detonated) {
                float urgency = Math.Clamp(1f - Projectile.ai[0] / 90f, 0f, 1f);
                float pulse = 1f + MathF.Sin(sealPhase * 4f) * 0.3f * urgency;

                // === LightningBranch 旋转符文圈（多个围绕中心旋转） ===
                Texture2D branchTex = ACMAsset.LightningBranch;
                if (branchTex != null) {
                    Vector2 branchOrigin = branchTex.Size() / 2f;
                    int runes = 4;
                    for (int r = 0; r < runes; r++) {
                        float angle = sealPhase * 2f + MathHelper.TwoPi * r / runes;
                        float radius = 25f + urgency * 15f;
                        Vector2 runePos = drawPos + angle.ToRotationVector2() * radius;
                        Color runeColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, urgency) * (0.3f + urgency * 0.4f);
                        runeColor.A = 0;
                        float runeScale = (0.06f + urgency * 0.04f) * pulse;
                        sb.Draw(branchTex, runePos, null, runeColor, angle + MathHelper.PiOver2, branchOrigin, runeScale, SpriteEffects.None, 0f);
                    }
                }

                // === SoftGlow 中心光点 ===
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    Color markColor = AoshunHelper.ThunderPurple * (0.2f + urgency * 0.3f);
                    markColor.A = 0;
                    sb.Draw(glowTex, drawPos, null, markColor, 0f, glowOrigin, (0.5f + urgency * 0.3f) * pulse, SpriteEffects.None, 0f);
                }
            }
            else {
                float fade = Projectile.timeLeft / 15f;
                Vector2 pillarBase = detonationCenter - Main.screenPosition;

                // === SlashBurst 底部爆发放射线条 ===
                Texture2D burstTex = ACMAsset.SlashBurst;
                if (burstTex != null) {
                    Vector2 burstOrigin = new(burstTex.Width / 2f, burstTex.Height);
                    Color burstColor = AoshunHelper.ElectricWhite * fade * 0.7f;
                    burstColor.A = 0;
                    float burstScale = 0.4f * fade;
                    sb.Draw(burstTex, pillarBase, null, burstColor, 0f, burstOrigin, burstScale, SpriteEffects.None, 0f);
                    // 翻转叠加
                    sb.Draw(burstTex, pillarBase, null, burstColor * 0.5f, MathHelper.Pi, new Vector2(burstTex.Width / 2f, 0), burstScale * 0.8f, SpriteEffects.None, 0f);
                }

                // === 顶点TriangleStrip绘制雷柱 ===
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Texture2D pillarTex = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                int pillarSegments = 25;
                float pillarHeight = 800f;

                // 外层雷柱
                List<ColoredVertex> pillarVerts = new();
                for (int s = 0; s <= pillarSegments; s++) {
                    float t = (float)s / pillarSegments;
                    Vector2 segPos = pillarBase + new Vector2(0, -t * pillarHeight);
                    float wave = MathF.Sin(sealPhase * 5f + t * 8f) * (10f + t * 5f);
                    segPos.X += wave;
                    float width = (40f - t * 25f) * fade;

                    Color c = Color.Lerp(AoshunHelper.LightningBlue * 0.8f, AoshunHelper.ThunderPurple * 0.5f, t) * fade;
                    pillarVerts.Add(new ColoredVertex(segPos + new Vector2(-width, 0), new Vector3(t, 0, 1), c));
                    pillarVerts.Add(new ColoredVertex(segPos + new Vector2(width, 0), new Vector3(t, 1, 1), c));
                }
                if (pillarVerts.Count >= 3) {
                    gd.Textures[0] = pillarTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, pillarVerts.ToArray(), 0, pillarVerts.Count - 2);
                }

                // 内层亮芯
                List<ColoredVertex> innerPillar = new();
                for (int s = 0; s <= pillarSegments; s++) {
                    float t = (float)s / pillarSegments;
                    Vector2 segPos = pillarBase + new Vector2(0, -t * pillarHeight);
                    float wave = MathF.Sin(sealPhase * 5f + t * 8f) * (10f + t * 5f);
                    segPos.X += wave;
                    float width = (15f - t * 10f) * fade;
                    Color c = AoshunHelper.ElectricWhite * 0.9f * fade;
                    innerPillar.Add(new ColoredVertex(segPos + new Vector2(-width, 0), new Vector3(t, 0, 1), c));
                    innerPillar.Add(new ColoredVertex(segPos + new Vector2(width, 0), new Vector3(t, 1, 1), c));
                }
                if (innerPillar.Count >= 3) {
                    gd.Textures[0] = VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerPillar.ToArray(), 0, innerPillar.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                // === Sparkle 底部爆炸火花 ===
                Texture2D sparkleTex = ACMAsset.Sparkle;
                if (sparkleTex != null) {
                    Vector2 sparkleOrigin = sparkleTex.Size() / 2f;
                    Color sparkColor = AoshunHelper.ElectricWhite * fade * 0.6f;
                    sparkColor.A = 0;
                    sb.Draw(sparkleTex, pillarBase, null, sparkColor, sealPhase * 3f, sparkleOrigin, 0.5f * fade, SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }

    #endregion

    #region 5. 冲击波 - 顶点拖尾 + LightShot弹体

    /// <summary>
    /// 冲击波 - 从Boss向外扩散的环形弹幕
    /// 绘制：ColoredVertex TriangleStrip拖尾 + LightShot灰度图弹体
    /// </summary>
    public class AoshunShockwave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wavePhase;
        private float trailOffset;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            wavePhase += 0.1f;
            trailOffset += 0.05f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.98f;

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.15f;
            }
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(wavePhase * 3f) * 0.2f;

            // === 顶点TriangleStrip拖尾（Additive混合） ===
            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                List<ColoredVertex> vertices = new();
                int count = Projectile.oldPos.Length;
                for (int i = 0; i < count; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) break;
                    float t = (float)i / count;
                    float scaleFactor = 1f - t;
                    Vector2 basePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Vector2 dir = (i < count - 1 && Projectile.oldPos[i + 1] != Vector2.Zero)
                        ? (Projectile.oldPos[i] - Projectile.oldPos[i + 1]).SafeNormalize(Vector2.UnitX)
                        : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perpDir = new(-dir.Y, dir.X);
                    float width = 16f * scaleFactor * pulse;
                    Color c = Color.Lerp(AoshunHelper.LightningBlue * 0.7f, AoshunHelper.ThunderPurple * 0.3f, t);
                    c.A = 0;
                    vertices.Add(new ColoredVertex(basePos + perpDir * width, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePos - perpDir * width, new Vector3(t + trailOffset, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // === LightShot灰度图弹体 ===
            Texture2D shotTex = ACMAsset.LightShot;
            if (shotTex != null) {
                Vector2 shotOrigin = shotTex.Size() / 2f;
                Color shotColor = AoshunHelper.LightningBlue * 0.8f * pulse;
                shotColor.A = 0;
                sb.Draw(shotTex, drawPos, null, shotColor, Projectile.rotation, shotOrigin, 0.5f * pulse, SpriteEffects.None, 0f);
                // 白高光核心
                Color coreColor = AoshunHelper.ElectricWhite * 0.6f;
                coreColor.A = 0;
                sb.Draw(shotTex, drawPos, null, coreColor, Projectile.rotation, shotOrigin, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 6. 风暴之眼 - 顶点环形风暴壁 + Smoke体积云

    /// <summary>
    /// 风暴之眼 - 以固定位置为中心的缩小安全区
    /// 绘制：ColoredVertex TriangleStrip闭合环绘制风暴壁
    ///       Smoke帧动画在风暴壁上叠加体积云
    ///       ElectricArcSheet在壁面穿插闪电
    /// </summary>
    public class AoshunStormEye : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxRadius = 700f;
        private const float MinRadius = 200f;

        private float stormPhase;
        private float currentRadius;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            stormPhase += 0.05f;
            Projectile.velocity = Vector2.Zero;

            int totalDuration = (int)Projectile.ai[0];
            if (totalDuration <= 0) totalDuration = 240;
            int elapsed = totalDuration - Projectile.timeLeft + (400 - totalDuration);

            float progress = Math.Clamp((float)elapsed / totalDuration, 0f, 1f);
            currentRadius = MathHelper.Lerp(MaxRadius, MinRadius, AoshunHelper.SineInOut(progress));

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                float dist = Vector2.Distance(Projectile.Center, p.Center);
                if (dist > currentRadius) {
                    float overflowRatio = Math.Clamp((dist - currentRadius) / 200f, 0f, 1f);
                    int dmg = (int)(10 + overflowRatio * 30);
                    if (Main.GameUpdateCount % 15 == 0) {
                        p.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                            p.name + " 被北海风暴吞噬"), dmg, 0);
                    }
                    Vector2 push = (Projectile.Center - p.Center).SafeNormalize(Vector2.Zero) * 0.5f;
                    p.velocity += push;
                }
            }

            if (!VaultUtils.isServer) {
                int particleCount = Math.Max((int)(currentRadius / 50f), 4);
                for (int i = 0; i < particleCount; i++) {
                    float angle = stormPhase * 2f + MathHelper.TwoPi * i / particleCount;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * currentRadius + Main.rand.NextVector2Circular(15, 15);
                    var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool(3) ? DustID.Electric : DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f);
            if (Projectile.timeLeft <= 400 - totalDuration) Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // === 顶点TriangleStrip闭合环绘制风暴壁(Additive混合) ===
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D wallTex = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;

            // 3层风暴壁
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = currentRadius + ring * 20f;
                float ringAlpha = 0.4f - ring * 0.1f;
                float ringRot = stormPhase * (2f + ring * 0.5f) * (ring % 2 == 0 ? 1 : -1);
                float halfWidth = 25f - ring * 6f;

                List<ColoredVertex> wallVerts = new();
                int wallSegments = 48;
                for (int s = 0; s <= wallSegments; s++) {
                    float t = (float)s / wallSegments;
                    float angle = ringRot + MathHelper.TwoPi * t;
                    Vector2 circlePoint = drawPos + angle.ToRotationVector2() * ringRadius;
                    // 法线方向（指向外）
                    Vector2 outward = angle.ToRotationVector2();

                    Color c = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.LightningBlue, ring / 2f) * ringAlpha;
                    wallVerts.Add(new ColoredVertex(circlePoint + outward * halfWidth, new Vector3(t * 4f + stormPhase, 0, 1), c));
                    wallVerts.Add(new ColoredVertex(circlePoint - outward * halfWidth, new Vector3(t * 4f + stormPhase, 1, 1), c));
                }

                if (wallVerts.Count >= 3) {
                    gd.Textures[0] = wallTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, wallVerts.ToArray(), 0, wallVerts.Count - 2);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // === Smoke帧动画在壁面叠加体积云 ===
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex != null) {
                int cloudCount = 8;
                int frameW = smokeTex.Width / 4;
                int frameH = smokeTex.Height / 4;
                Vector2 smokeOrigin = new(frameW / 2f, frameH / 2f);

                for (int c = 0; c < cloudCount; c++) {
                    float angle = stormPhase * 1.5f + MathHelper.TwoPi * c / cloudCount;
                    Vector2 cloudPos = drawPos + angle.ToRotationVector2() * currentRadius;
                    int frame = ((int)(stormPhase * 4f + c * 3)) % 16;
                    int col = frame % 4;
                    int row = frame / 4;
                    Rectangle smokeRect = new(col * frameW, row * frameH, frameW, frameH);

                    Color cloudColor = AoshunHelper.StormGray * 0.4f;
                    cloudColor.A = 100;
                    float cloudScale = 0.2f + MathF.Sin(stormPhase + c) * 0.03f;
                    sb.Draw(smokeTex, cloudPos, smokeRect, cloudColor, angle + stormPhase, smokeOrigin, cloudScale, SpriteEffects.None, 0f);
                }
            }

            // === ElectricArcSheet在壁面穿插闪电 ===
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcCount = 4;
                int arcH = arcSheet.Height / 4;
                for (int a = 0; a < arcCount; a++) {
                    float angle = stormPhase * 2.5f + MathHelper.TwoPi * a / arcCount;
                    Vector2 arcPos = drawPos + angle.ToRotationVector2() * (currentRadius + 5f);

                    int arcIdx = ((int)(stormPhase * 8f + a * 2)) % 4;
                    Rectangle arcRect = new(0, arcIdx * arcH, arcSheet.Width, arcH);
                    Vector2 arcOrigin = new(arcRect.Width / 2f, arcRect.Height / 2f);

                    Color arcColor = AoshunHelper.LightningBlue * 0.3f;
                    arcColor.A = 0;
                    float arcScale = 0.15f + MathF.Sin(stormPhase * 3f + a) * 0.03f;
                    sb.Draw(arcSheet, arcPos, arcRect, arcColor, angle + MathHelper.PiOver2 + stormPhase, arcOrigin, arcScale, SpriteEffects.None, 0f);
                }
            }

            // === 中心安全区SoftGlow提示 ===
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color safeColor = AoshunHelper.NorthSeaCyan * 0.08f;
                safeColor.A = 0;
                float safeScale = currentRadius / (glowTex.Width * 0.5f);
                sb.Draw(glowTex, drawPos, null, safeColor, 0f, glowOrigin, safeScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * currentRadius;
                var d = Dust.NewDustPerfect(pos, DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 8f;
                d.scale = 2.5f;
            }
        }
    }

    #endregion

    #region 7. 电痕 - ElectricArcSheet帧动画 + 顶点地面电场

    /// <summary>
    /// 持续电痕 - 留在地面的静态伤害区域
    /// 绘制：ElectricArcSheet帧动画覆盖区域
    ///       SoftGlow底层光晕 + Sparkle偶尔火花
    /// </summary>
    public class AoshunElectricTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float trailPhase;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            trailPhase += 0.08f;
            Projectile.velocity = Vector2.Zero;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 15);
                var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1f + Main.rand.NextFloat(0.5f);
                d.velocity = Main.rand.NextVector2Circular(1, 1);
            }

            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f * fade);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            float pulse = 1f + MathF.Sin(trailPhase * 4f) * 0.2f;

            // === SoftGlow 底层光晕 ===
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color baseColor = AoshunHelper.ThunderPurple * 0.25f * fade;
                baseColor.A = 0;
                sb.Draw(glowTex, drawPos, null, baseColor, 0f, glowOrigin, 0.8f * pulse, SpriteEffects.None, 0f);
            }

            // === ElectricArcSheet帧动画（主要视觉） ===
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcIdx = ((int)(trailPhase * 6f)) % 4;
                int arcH = arcSheet.Height / 4;
                Rectangle arcRect = new(0, arcIdx * arcH, arcSheet.Width, arcH);
                Vector2 arcOrigin = new(arcRect.Width / 2f, arcRect.Height / 2f);

                // 主电弧
                Color arcColor = AoshunHelper.LightningBlue * 0.5f * fade * pulse;
                arcColor.A = 0;
                sb.Draw(arcSheet, drawPos, arcRect, arcColor, trailPhase * 1.2f, arcOrigin, 0.1f * pulse, SpriteEffects.None, 0f);
                // 旋转叠加
                Color arcColor2 = AoshunHelper.ThunderPurple * 0.3f * fade;
                arcColor2.A = 0;
                sb.Draw(arcSheet, drawPos, arcRect, arcColor2, -trailPhase * 0.8f + MathHelper.PiOver4, arcOrigin, 0.08f, SpriteEffects.FlipHorizontally, 0f);
            }

            // === Sparkle 偶尔火花闪烁 ===
            Texture2D sparkleTex = ACMAsset.Sparkle;
            if (sparkleTex != null && MathF.Sin(trailPhase * 5f) > 0.7f) {
                Vector2 sparkleOrigin = sparkleTex.Size() / 2f;
                Color sparkColor = AoshunHelper.ElectricWhite * 0.35f * fade;
                sparkColor.A = 0;
                sb.Draw(sparkleTex, drawPos, null, sparkColor, trailPhase * 3f, sparkleOrigin, 0.15f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion
}

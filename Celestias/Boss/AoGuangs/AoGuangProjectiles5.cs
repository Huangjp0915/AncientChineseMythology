using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region 行走水龙卷

    /// <summary>
    /// 行走水龙卷 (V3 新增) - 龙王甩尾掷出的地面水龙卷: 落点 30f 红标 → 成柱 →
    /// 沿初始方向缓速平移 (2.6px/f, 无追踪, 可跳越)。生成时贴地, 行走中持续贴地。
    /// </summary>
    public class AoGuangWaterspout : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Timer => ref Projectile.ai[0];

        private float spoutHeight = 0f;
        private float spoutAlpha = 0f;
        private float spin;
        private const float MaxHeight = 860f;
        private const int TelegraphTime = 30;
        private const int LifeTime = 420;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
            // 简中默认名走本地化文件; 这里兜底注册英文默认
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.AoGuangWaterspout.DisplayName",
                () => "Roving Waterspout");
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            Timer++;
            spin += 0.16f;

            // 首帧记下行走速度 (预警期要清零 velocity)
            if ((int)Timer == 1)
                Projectile.localAI[1] = Projectile.velocity.X;

            // 落点红标期: 静止, 无伤害
            if (Timer < TelegraphTime) {
                Projectile.velocity = Vector2.Zero;
                if (Main.netMode != NetmodeID.Server && (int)Timer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 0), 0, 0,
                        DustID.RedTorch, 0, -2.5f, 140, TelegraphColors.Lethal, 1.5f);
                    d.noGravity = true;
                }
                return;
            }

            if ((int)Timer == TelegraphTime) {
                // 拔地而起, 恢复行走
                Projectile.velocity = new Vector2(Projectile.localAI[1], 0f);
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.45f, Volume = 1.1f }, Projectile.Center);
                ACMUtils.AddScreenShake(5f);
            }

            // 成柱 / 末段消散
            if (Projectile.timeLeft > 40) {
                spoutAlpha = MathHelper.Lerp(spoutAlpha, 1f, 0.06f);
                spoutHeight = MathHelper.Lerp(spoutHeight, MaxHeight, 0.05f);
            }
            else {
                spoutAlpha = Projectile.timeLeft / 40f;
            }

            // 缓速平移 + 贴地 (每 10f 重新探地, 适应地形起伏)
            if ((int)Timer % 10 == 0) {
                float groundY = AoGuang.FindGroundY(Projectile.Center.X, Projectile.Center.Y - 300f);
                Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, groundY, 0.5f));
            }

            // 龙卷粒子: 沿柱身螺旋上升
            if (Main.netMode != NetmodeID.Server && spoutAlpha > 0.3f) {
                for (int i = 0; i < 3; i++) {
                    float h = Main.rand.NextFloat(0f, spoutHeight);
                    float ang = spin * 2f + h * 0.02f;
                    float radius = 26f + h / spoutHeight * 44f;
                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(ang) * radius, -h);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.9f);
                    d.noGravity = true;
                    d.velocity = new Vector2(MathF.Cos(ang + MathHelper.PiOver2) * 5f, -2.5f);
                }
            }

            Lighting.AddLight(Projectile.Center + new Vector2(0, -spoutHeight * 0.4f),
                AoGuangHelper.OceanTeal.ToVector3() * spoutAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 柱形碰撞: 成柱后才有伤害 (与视觉严格对齐)
            if (spoutAlpha < 0.75f)
                return false;
            float dx = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float ty = targetHitbox.Center.Y;
            bool inHeight = ty < Projectile.Center.Y + 30f && ty > Projectile.Center.Y - spoutHeight;
            return dx < 52f && inHeight;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 落点红标 (Lethal 地面横线)
            if (Timer < TelegraphTime && ACMAsset.GlaciateWave != null) {
                float warnT = Timer / (float)TelegraphTime;
                Color warn = TelegraphColors.Lethal * (0.3f + warnT * 0.5f);
                warn.A = 0;
                Vector2 origin0 = new Vector2(0, ACMAsset.GlaciateWave.Height / 2f);
                Main.spriteBatch.Draw(ACMAsset.GlaciateWave, screenPos - new Vector2(60f, 0), null, warn,
                    0f, origin0, new Vector2(120f / ACMAsset.GlaciateWave.Width, 0.08f), SpriteEffects.None, 0f);
            }

            if (spoutHeight < 10f)
                return false;

            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 分段龙卷 (28 段 ×2 层): 底粗顶细, 顶端甩尾
            int segments = 28;
            for (int seg = 0; seg < segments; seg++) {
                float hp = seg / (float)segments;
                float yOffset = -hp * spoutHeight;
                float segRadius = MathHelper.Lerp(1.7f, 0.7f, hp) * spoutAlpha;
                float segRot = spin + seg * 0.85f;
                float wobble = MathF.Sin(spin * 1.9f + seg * 0.6f) * (6f + hp * 26f);

                Vector2 segPos = screenPos + new Vector2(wobble, yOffset);

                Color outer = AoGuangHelper.OceanTeal * spoutAlpha * 0.45f;
                outer.A = 0;
                sb.Draw(tornadoTex, segPos, null, outer, segRot, origin, segRadius * 1.2f, SpriteEffects.None, 0f);

                Color mid = AoGuangHelper.DragonBlue * spoutAlpha * 0.5f;
                mid.A = 0;
                sb.Draw(tornadoTex, segPos, null, mid, -segRot * 1.15f, origin, segRadius * 0.8f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 24; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-9f, -1f));
                Dust d = Dust.NewDustDirect(Projectile.Center + new Vector2(0, -Main.rand.NextFloat(0, 200f)), 0, 0,
                    Main.rand.NextBool() ? DustID.Water : DustID.Wet, vel.X, vel.Y, 120, default, 2f);
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
        }
    }

    #endregion

    #region 天倾巨浪

    /// <summary>
    /// 天倾巨浪 (V3 新增) - 终潮天倾的半场坠浪: 60f 半场 Lethal 幕布预警 (静止) →
    /// 30px/f 天倾坠落横扫半场 → 落底消散。ai0 = 危险半场方向 (+1 分界线右侧 / -1 左侧),
    /// ai1 = 参考地面 Y (坠过该线 220px 后消散)。分界线 X = 生成点 X (竖直下落不变)。
    /// </summary>
    public class AoGuangSkyDeluge : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float HalfDir => ref Projectile.ai[0];
        private ref float GroundRefY => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        private const int WarnTime = 60;
        private const float HalfThick = 120f;  // 浪体半厚 (竖向)
        private const float HalfSpan = 1250f;  // 危险半场覆盖宽度

        private float intensity;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.AoGuangSkyDeluge.DisplayName",
                () => "Skyfall Deluge");
        }

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 240;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + 170;
        }

        public override void AI() {
            Timer++;

            if (Timer <= WarnTime) {
                // 预警期: 悬停不动, 幕布渐显
                Projectile.velocity = Vector2.Zero;
                intensity = MathHelper.Lerp(intensity, 0.85f, 0.08f);
                return;
            }

            if ((int)Timer == WarnTime + 1) {
                Projectile.velocity = new Vector2(0f, 30f); // 单帧点火坠落
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.1f, Volume = 1.5f }, Projectile.Center);
            }

            intensity = MathHelper.Lerp(intensity, 1f, 0.2f);

            // 浪底飞沫
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float x = Projectile.Center.X + HalfDir * Main.rand.NextFloat(30f, HalfSpan * 0.9f);
                    Dust d = Dust.NewDustDirect(new Vector2(x, Projectile.Center.Y + HalfThick * 0.8f), 0, 0,
                        Main.rand.NextBool() ? DustID.Water : DustID.Wet, 0, 8f, 110, default, 2.2f);
                    d.noGravity = true;
                }
            }

            // 坠过地面参考线后消散
            if (Projectile.Center.Y > GroundRefY + 220f) {
                Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center + new Vector2(HalfDir * 300f, 0), AoGuangHelper.DragonBlue.ToVector3() * intensity);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 预警期无伤害; 坠落期: 危险半场 + 浪体厚度带
            if (Timer <= WarnTime)
                return false;
            float relX = (targetHitbox.Center.X - Projectile.Center.X) * HalfDir;
            if (relX < 0f || relX > HalfSpan)
                return false;
            return MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y) < HalfThick * 0.85f;
        }

        public override bool PreDraw(ref Color lightColor) {
            bool shaderOk = AoGuangHelper.TidalWallEffect != null && MythologyConfig.FullscreenShadersEnabled;

            if (Timer <= WarnTime) {
                // 半场预警幕布: uDir 指向安全侧 (shader 中 s<0 为危险区)
                if (shaderOk) {
                    AoGuangHelper.DrawTidalWallDecal(Main.spriteBatch,
                        Projectile.Center, new Vector2(-HalfDir, 0f), 0f,
                        Projectile.Center, 0f, intensity, warnOnly: true);
                }
                return false;
            }

            if (shaderOk) {
                // 坠落浪体: 竖直向下的整面浪, 半场遮罩留出安全侧
                AoGuangHelper.DrawTidalWallDecal(Main.spriteBatch,
                    Projectile.Center, new Vector2(0f, 1f), HalfThick,
                    Projectile.Center, 0f, intensity,
                    warnOnly: false, halfDir: new Vector2(HalfDir, 0f));
                return false;
            }

            // —— CPU 回退: 横向水带 ——
            if (ACMAsset.GlaciateWave == null) return false;
            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            for (int layer = 2; layer >= 0; layer--) {
                Color c = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    _ => AoGuangHelper.OceanTeal
                };
                c *= (0.7f - layer * 0.15f) * intensity;
                c.A = 0;
                Vector2 scale = new Vector2(HalfSpan / tex.Width, 0.3f + layer * 0.12f);
                Main.spriteBatch.Draw(tex, screenPos, null, c, HalfDir > 0 ? 0f : MathHelper.Pi, origin, scale,
                    SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 36; i++) {
                float x = Projectile.Center.X + HalfDir * Main.rand.NextFloat(0f, HalfSpan * 0.8f);
                Dust d = Dust.NewDustDirect(new Vector2(x, Projectile.Center.Y), 0, 0,
                    Main.rand.NextBool() ? DustID.Water : DustID.BubbleBlock,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-9f, -2f), 100, default, 2.4f);
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.3f }, Projectile.Center);
            ACMUtils.AddScreenShake(9f);
        }
    }

    #endregion
}

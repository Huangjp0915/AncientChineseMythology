using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙青蓝雷弹 — 替换原版 CultistBossLightningOrb* 占位弹。
    /// 青蓝主体 + 电弧拖尾 + 柔光核心 (toolkit §C.1 雷=青白电弧)。直线飞行的主题弹。
    /// 贴图复用已存在的 Textures/Projectiles/ThunderOrb (自动加载安全)。
    /// </summary>
    internal class AzureBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        // ai[0]: 自旋相位种子 (纯视觉)
        private static readonly Color Cyan = AzureDragon.DragonCyan;
        private static readonly Color Lightning = AzureDragon.DragonLightning;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 260;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.ai[1]++;
            if (Projectile.velocity.LengthSquared() > 0.04f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, Cyan.ToVector3() * 0.7f);

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 off = Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                int d = Dust.NewDust(Projectile.Center + off, 0, 0, dustType, 0, 0, 80, default, 1.3f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -Projectile.velocity * 0.12f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 电弧拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = i / (float)Projectile.oldPos.Length;
                Color trail = Color.Lerp(Lightning, AzureDragon.DragonDeep, t) * (0.5f * (1f - t));
                trail.A = 0;
                Vector2 tp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(tex, tp, null, trail, Projectile.oldRot[i], origin, (1f - t * 0.5f), SpriteEffects.None, 0f);
            }

            // 柔光核心
            if (ACMAsset.SoftGlow != null) {
                Vector2 gOrigin = new(ACMAsset.SoftGlow.Width / 2f, ACMAsset.SoftGlow.Height / 2f);
                float pulse = 0.8f + 0.2f * MathF.Sin(Projectile.ai[1] * 0.4f);
                Color glow = Cyan * (0.6f * pulse); glow.A = 0;
                sb.Draw(ACMAsset.SoftGlow, pos, null, glow, 0f, gOrigin, 0.5f * pulse, SpriteEffects.None, 0f);
                Color white = Color.White * (0.35f * pulse); white.A = 0;
                sb.Draw(ACMAsset.SoftGlow, pos, null, white, 0f, gOrigin, 0.26f * pulse, SpriteEffects.None, 0f);
            }

            // 主体
            Color body = Color.Lerp(Cyan, Color.White, 0.35f); body.A = 0;
            sb.Draw(tex, pos, null, body, Projectile.rotation, origin, 1f, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }

    /// <summary>
    /// 苍龙雷霆落雷柱 — 可读的"标记落点 → 约 1.5s 后劈下"地面雷柱 (toolkit §C.1 AoE: 形状+颜色+渐强时间)。
    /// 既用于一阶段地面雷柱预告, 也用作 P3「网格化雷霆审判庭」的格点雷柱。
    /// 预告期: 青蓝竖直警示束 (末段转致命红); 命中期: 过曝雷柱 (DrawBeam) + 落点泛光 (DrawRadialBloomAt) + 屏震。
    /// 命中盒为竖直长柱 → 强制玩家横向走位躲到安全列。贴图复用 ThunderOrb (自动加载安全)。
    /// </summary>
    internal class AzureThunderRod : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        // ai[0]=预告 tick, ai[1]=命中活跃 tick; localAI[0]=计时
        private const int ColumnHalfHeight = 1300;

        private float Timer => Projectile.localAI[0];
        private int Telegraph => (int)Projectile.ai[0];
        private int StrikeActive => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 6000;
        }

        public override void SetDefaults() {
            Projectile.width = 58;
            Projectile.height = ColumnHalfHeight * 2;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            // 默认参数兜底
            if (Telegraph <= 0) Projectile.ai[0] = 90;
            if (StrikeActive <= 0) Projectile.ai[1] = 16;

            if ((int)Timer == Telegraph) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(6f);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 40; i++) {
                        Vector2 v = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-22f, 22f));
                        int dt = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                        int d = Dust.NewDust(Projectile.Center + new Vector2(0, Main.rand.NextFloat(-ColumnHalfHeight, ColumnHalfHeight)), 0, 0, dt, v.X, v.Y, 40, default, 2.2f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }

            if (Timer >= Telegraph + StrikeActive)
                Projectile.Kill();

            Lighting.AddLight(Projectile.Center, AzureDragon.DragonCyan.ToVector3() * (Timer >= Telegraph ? 1.4f : 0.4f));
        }

        public override bool CanHitPlayer(Player target) {
            return Timer >= Telegraph && Timer < Telegraph + StrikeActive;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            Vector2 top = Projectile.Center - new Vector2(0, ColumnHalfHeight);
            Vector2 bottom = Projectile.Center + new Vector2(0, ColumnHalfHeight);

            bool striking = Timer >= Telegraph;
            if (!striking) {
                float p = MathHelper.Clamp(Timer / MathF.Max(Telegraph, 1), 0f, 1f);
                // 渐强 + 末段转致命红 (预警三要素: 形状=竖柱, 颜色=青→红, 时间=渐强)
                Color warn = Color.Lerp(AzureDragon.DragonCyan, TelegraphColors.Lethal, p * p);
                float flick = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(Timer * 0.6f));
                float hw = MathHelper.Lerp(3f, 9f, p) * flick;
                ACMShaders.DrawBeam(top, bottom, hw, warn * 0.9f, warn * 0.3f, 0.35f + 0.5f * p,
                    flowSpeed: 2.2f, flowScale: 3.5f, coreSharp: 2.6f);
            }
            else {
                int s = (int)(Timer - Telegraph);
                float fade = 1f;
                if (s < 3) fade = (s + 1) / 3f;
                else if (s > StrikeActive - 5) fade = MathF.Max(0f, (StrikeActive - s) / 5f);

                Color core = Color.Lerp(AzureDragon.DragonLightning, Color.White, 0.6f);
                ACMShaders.DrawBeam(top, bottom, 34f * fade, core, AzureDragon.DragonCyan, fade,
                    flowSpeed: 3.5f, flowScale: 2.5f, coreSharp: 2.0f, coreGlow: 1.4f);
                ACMShaders.DrawBeam(top, bottom, 14f * fade, Color.White, core, fade,
                    flowSpeed: 4.5f, flowScale: 4.0f, coreSharp: 3.0f, coreGlow: 2.0f);

                // 落点过曝泛光 (内部自动占用本帧唯一全屏名额, 多柱只首个生效)
                ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.13f * fade, fade,
                    AzureDragon.DragonLightning, rayCount: 0f, falloff: 2.8f);
            }
            return false;
        }
    }
}

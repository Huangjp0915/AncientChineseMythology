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
    /// 苍龙青蓝雷弹 — 主题直线弹。
    /// ai[0] &gt; 0 时为弹速爬升帧数: 前 N 帧从 30% 速度爬到 100% (换招/新星公平阀门);
    /// ai[1] 为寿命计时 (视觉相位)。
    /// </summary>
    internal class AzureBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

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
            Projectile.timeLeft = 300;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.ai[1]++;

            // 弹速爬升 (发射瞬间只有 30% 速度, 玩家有读弹时间)
            if (Projectile.ai[0] > 0f) {
                if (Projectile.localAI[0] == 0f)
                    Projectile.localAI[0] = Projectile.velocity.Length();
                float t = MathHelper.Clamp(Projectile.ai[1] / Projectile.ai[0], 0.3f, 1f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * (Projectile.localAI[0] * t);
            }

            if (Projectile.velocity.LengthSquared() > 0.04f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, Cyan.ToVector3() * 0.7f);

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 off = Main.rand.NextVector2Circular(8f, 8f);
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
                sb.Draw(tex, tp, null, trail, Projectile.oldRot[i], origin, 1f - t * 0.5f, SpriteEffects.None, 0f);
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
    /// 苍龙雷霆落雷柱 — 可读的"标记 → 渐强 → 劈下"光束柱。
    /// V3 泛化为任意朝向: ai[2] 为角度偏移 (0=竖直, PiOver2=水平), 命中判定为线段碰撞。
    /// ai[0]=预告 tick, ai[1]=命中活跃 tick。预告末段转致命红 (预警三要素: 形状+颜色+渐强时间)。
    /// </summary>
    internal class AzureThunderRod : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        private const int ColumnHalfLength = 1300;

        private float Timer => Projectile.localAI[0];
        private int Telegraph => (int)Projectile.ai[0];
        private int StrikeActive => (int)Projectile.ai[1];

        /// <summary>光束方向 (ai2=0 时竖直向下)。</summary>
        private Vector2 BeamDir => (Projectile.ai[2] + MathHelper.PiOver2).ToRotationVector2();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 6000;
        }

        public override void SetDefaults() {
            Projectile.width = 58;
            Projectile.height = 58;
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
            Projectile.velocity = Vector2.Zero;

            // 默认参数兜底
            if (Telegraph <= 0) Projectile.ai[0] = 90;
            if (StrikeActive <= 0) Projectile.ai[1] = 16;

            if ((int)Timer == Telegraph) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(6f);
                if (!VaultUtils.isServer) {
                    Vector2 dir = BeamDir;
                    Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                    for (int i = 0; i < 40; i++) {
                        Vector2 basePos = Projectile.Center + dir * Main.rand.NextFloat(-ColumnHalfLength, ColumnHalfLength);
                        Vector2 v = perp * Main.rand.NextFloat(-5f, 5f) + dir * Main.rand.NextFloat(-20f, 20f);
                        int dt = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                        int d = Dust.NewDust(basePos, 0, 0, dt, v.X, v.Y, 40, default, 2.2f);
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

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < Telegraph || Timer >= Telegraph + StrikeActive)
                return false;
            Vector2 dir = BeamDir;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - dir * ColumnHalfLength, Projectile.Center + dir * ColumnHalfLength, 52f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 dir = BeamDir;
            Vector2 head = Projectile.Center - dir * ColumnHalfLength;
            Vector2 tail = Projectile.Center + dir * ColumnHalfLength;

            bool striking = Timer >= Telegraph;
            if (!striking) {
                float p = MathHelper.Clamp(Timer / MathF.Max(Telegraph, 1), 0f, 1f);
                // 渐强 + 末段转致命红
                Color warn = Color.Lerp(AzureDragon.DragonCyan, TelegraphColors.Lethal, p * p);
                float flick = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(Timer * 0.6f));
                float hw = MathHelper.Lerp(3f, 9f, p) * flick;
                ACMShaders.DrawBeam(head, tail, hw, warn * 0.9f, warn * 0.3f, 0.35f + 0.5f * p,
                    flowSpeed: 2.2f, flowScale: 3.5f, coreSharp: 2.6f);
            }
            else {
                int s = (int)(Timer - Telegraph);
                float fade = 1f;
                if (s < 3) fade = (s + 1) / 3f;
                else if (s > StrikeActive - 5) fade = MathF.Max(0f, (StrikeActive - s) / 5f);

                Color core = Color.Lerp(AzureDragon.DragonLightning, Color.White, 0.6f);
                ACMShaders.DrawBeam(head, tail, 34f * fade, core, AzureDragon.DragonCyan, fade,
                    flowSpeed: 3.5f, flowScale: 2.5f, coreSharp: 2.0f, coreGlow: 1.4f);
                ACMShaders.DrawBeam(head, tail, 14f * fade, Color.White, core, fade,
                    flowSpeed: 4.5f, flowScale: 4.0f, coreSharp: 3.0f, coreGlow: 2.0f);

                // 落点过曝泛光 (内部自动占用本帧唯一全屏名额, 多柱只首个生效)
                ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.13f * fade, fade,
                    AzureDragon.DragonLightning, rayCount: 0f, falloff: 2.8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 苍龙风暴雷珠 — 「风暴合围」沿环布设的充能雷珠。
    /// ai[0]=发射延迟(帧, 逐珠错相), ai[1]=计时。
    /// 充能语法: 汇聚粒子密度 ∝ √t 且 75% 截止, 发射前坍缩闪烁; 发射时向环心放 3 发扇形雷弹。
    /// </summary>
    internal class AzureStormOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        private float FireDelay => MathF.Max(Projectile.ai[0], 30f);
        private ref float Timer => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            float charge = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);

            Lighting.AddLight(Projectile.Center, AzureDragon.DragonCyan.ToVector3() * (0.4f + 0.6f * charge));

            // 汇聚粒子: 密度 ∝ √charge, 75% 截止 (最后一段是安静的吸气)
            if (!VaultUtils.isServer && charge < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(charge) * 0.8f) {
                Vector2 dp = Projectile.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 90, default, 1.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (Projectile.Center - dp) * 0.085f;
            }

            if ((int)Timer == (int)FireDelay) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int t = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                    if (t >= 0) {
                        Vector2 aim = (Main.player[t].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        for (int i = -1; i <= 1; i++) {
                            Vector2 vel = aim.RotatedBy(i * 0.21f) * 9f;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                                ModContent.ProjectileType<AzureBolt>(), Projectile.damage, 1f, ai0: 16f);
                        }
                    }
                }
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 18; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                        int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 50, default, 1.9f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }

            if (Timer >= FireDelay + 14f)
                Projectile.Kill();
        }

        /// <summary>只有充能过半的雷珠才有接触伤害 (视觉上它已明显危险)。</summary>
        public override bool CanHitPlayer(Player target) {
            return Timer > FireDelay * 0.5f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float charge = MathHelper.Clamp(Timer / FireDelay, 0f, 1f);

            // 发射前坍缩闪烁 (变小再变响)
            float scale = 0.55f + 0.45f * charge;
            if (charge > 0.85f) {
                float a = (charge - 0.85f) / 0.15f;
                scale *= MathHelper.SmoothStep(1f, MathF.Cos(Timer * 0.9f) * 0.07f + 0.55f, a);
            }
            float fireFade = Timer > FireDelay ? MathF.Max(0f, 1f - (Timer - FireDelay) / 14f) : 1f;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (ACMAsset.SoftGlow != null) {
                Vector2 gOrigin = ACMAsset.SoftGlow.Size() / 2f;
                Color glow = AzureDragon.DragonCyan * (0.65f * (0.3f + 0.7f * charge) * fireFade); glow.A = 0;
                sb.Draw(ACMAsset.SoftGlow, pos, null, glow, 0f, gOrigin, 1.1f * scale + 0.3f * charge, SpriteEffects.None, 0f);
                Color white = Color.White * (0.4f * charge * fireFade); white.A = 0;
                sb.Draw(ACMAsset.SoftGlow, pos, null, white, 0f, gOrigin, 0.5f * scale, SpriteEffects.None, 0f);
            }

            // 环绕电弧 (充能进度即状态广播)
            if (ACMAsset.ElectricArcSheet != null && charge > 0.25f) {
                Texture2D arc = ACMAsset.ElectricArcSheet;
                int arcH = arc.Height / 4;
                Rectangle src = new(0, ((int)(Timer * 0.5f) % 4) * arcH, arc.Width, arcH);
                Color arcCol = AzureDragon.DragonLightning * (0.5f * charge * fireFade); arcCol.A = 0;
                sb.Draw(arc, pos, src, arcCol, Timer * 0.15f, new Vector2(src.Width / 2f, src.Height / 2f),
                    0.16f * scale, SpriteEffects.None, 0f);
            }

            Color body = Color.Lerp(AzureDragon.DragonCyan, Color.White, 0.4f) * fireFade; body.A = 0;
            sb.Draw(tex, pos, null, body, Timer * 0.05f, tex.Size() / 2f, 0.4f * scale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }
}

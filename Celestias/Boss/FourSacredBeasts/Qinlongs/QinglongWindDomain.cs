using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙·风域 (V2 「风域天罚 Stormfield Judgment」机制载体) — 持续的<b>非伤害</b>位移力场。
    /// A persistent, non-damaging wind vortex: the local player inside the radius is swirled (tangential +
    /// slight inward), forcing counter-movement while telegraphed <see cref="QinglongThunderColumn"/> lethal
    /// columns rain in the gaps between domains ("逆风站位, 别被风推进雷里").
    ///
    /// 颜色=青龙青 (非致命, §6.1: 红只给致命雷柱); 边界经硬化 ACMShaders.ArenaRunic 地纹绘制 (缩放感知
    /// WorldDecalParams)。逻辑(推力)与表现(地纹)分离: 推力仅作用本地玩家(客户端响应), 服务端零绘制。
    /// ai[0] = 世界半径(像素); ai[1] = 旋向(+1/-1)。timeLeft 由施放者设定。
    /// </summary>
    public class QinglongWindDomain : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int FadeTicks = 36;

        private float WorldRadius => MathF.Max(120f, Projectile.ai[0]);
        private float SwirlDir => Projectile.ai[1] >= 0 ? 1f : -1f;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.localAI[0]++; // age

            // —— 力场: 仅推本地玩家(各客户端推自己, 避免与网络位置打架) ——
            if (!Main.dedServ) {
                Player p = Main.LocalPlayer;
                if (p != null && p.active && !p.dead) {
                    Vector2 toCenter = Projectile.Center - p.Center;
                    float dist = toCenter.Length();
                    float r = WorldRadius;
                    if (dist < r && dist > 1f) {
                        float falloff = 1f - dist / r;
                        Vector2 radial = toCenter / dist;
                        Vector2 tangent = new Vector2(-radial.Y, radial.X) * SwirlDir;
                        float power = 0.7f * falloff * FadeFactor();
                        // 切向旋流 + 轻微内吸 = 漩涡, 玩家需逆风修正走位
                        p.velocity += tangent * power + radial * (power * 0.25f);
                    }
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * WorldRadius * Main.rand.NextFloat(0.6f, 1f);
                Dust d = Dust.NewDustDirect(pos, 4, 4, DustID.GreenTorch, 0, 0, 150, default, 1.1f);
                d.noGravity = true;
                Vector2 toC = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                d.velocity = new Vector2(-toC.Y, toC.X) * SwirlDir * 2.4f;
            }

            Lighting.AddLight(Projectile.Center, 0.12f, 0.35f, 0.22f);
        }

        /// <summary>淡入淡出系数 (开头/结尾衰减力场与可视强度)。</summary>
        private float FadeFactor() {
            float age = Projectile.localAI[0];
            float inF = MathHelper.Clamp(age / FadeTicks, 0f, 1f);
            float outF = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
            return inF * outF;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return false;

            float intensity = 0.7f * FadeFactor();
            if (intensity <= 0.01f)
                return false;

            ACMShaders.WorldDecalParams(Projectile.Center, WorldRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            Color primary = TelegraphColors.AzureDragon;
            Color secondary = new Color(16, 90, 64);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly * SwirlDir);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            return false;
        }
    }
}

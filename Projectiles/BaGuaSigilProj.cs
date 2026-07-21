using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 八卦法阵 (BaGuaZhenpan 的场上表达层, 纯视觉不造成伤害)。
    /// 展开仪式: 48 帧弹性生长, 八卦按先天卦序逐位点亮 (音阶随之爬升);
    /// 常驻: 身后加性法阵盘, 阵法激活 (BaGuaPlayer.CurrentName 非空) 时提亮加速;
    /// 阵法切换瞬间重燃闪光; Buff 消失则 14 帧收拢消散。
    /// 阵法逻辑本体在 Players/BaGuaPlayer.cs (只读其公共字段, 不修改)。
    /// </summary>
    public class BaGuaSigilProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BaGuaSigilProj";

        private const int UnfoldTime = 48;   // 展开帧数
        private const int CollapseTime = 14; // 收拢帧数
        private const float WorldRadius = 100f; // 外环世界半径 (像素)

        // 主题色 (金 × 玄青)
        private static readonly Color GoldPrimary = new(255, 215, 130);
        private static readonly Color AzureSecondary = new(64, 138, 152);

        // —— 以下全部为纯视觉状态 (逐客户端独立, 不参与 gameplay 同步) ——
        private float _unfold;          // 0→1 展开进度
        private float _collapse;        // 0→1 收拢进度
        private float _spin;            // 盘面自转角
        private float _activeGlow;      // 阵法激活态平滑值
        private float _reigniteFlash;   // 阵法切换重燃闪光
        private int _litCount;          // 已点亮卦数 (用于音阶)
        private bool _unfoldDone;       // 展开完成帧 (冲击环 + 完成音)
        private string _lastFormation = "";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480; // 法阵略出屏仍绘制
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false; // 纯表达层

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            bool alive = owner.active && !owner.dead && owner.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>());

            if (!alive) {
                // 收拢消散
                _collapse += 1f / CollapseTime;
                if (_collapse >= 1f) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                Projectile.timeLeft = 2;
                _collapse = 0f;
            }

            Projectile.Center = owner.MountedCenter;

            // —— 展开进度 + 逐卦点亮音阶 (一乾二兑三离…八声爬升) ——
            if (_unfold < 1f) {
                _unfold = Math.Min(1f, _unfold + 1f / UnfoldTime);
                int lit = (int)(_unfold * 8f);
                if (lit > _litCount && !Main.dedServ) {
                    for (int i = _litCount; i < lit; i++)
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.32f, Pitch = -0.25f + i * 0.09f }, Projectile.Center);
                    _litCount = lit;
                }
                if (_unfold >= 1f && !_unfoldDone) {
                    _unfoldDone = true;
                    _reigniteFlash = 1f; // 完成帧: 复用重燃闪光通道 (提亮 + 外扩环)
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = 0.15f }, Projectile.Center);
                        WeaponVFX.AddScreenShake(Projectile.Center, 2f);
                    }
                }
            }

            // —— 阵法激活态 (只读 BaGuaPlayer 公共字段; 其他客户端读到空串则维持基础态, 纯视觉可接受) ——
            string formation = owner.GetModPlayer<Players.BaGuaPlayer>().CurrentName ?? "";
            bool active = formation.Length > 0;
            _activeGlow = MathHelper.Lerp(_activeGlow, active ? 1f : 0f, 0.06f);

            if (formation != _lastFormation) {
                if (_lastFormation.Length > 0 || active) {
                    _reigniteFlash = 1f;
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = active ? 0.35f : -0.4f }, Projectile.Center);
                }
                _lastFormation = formation;
            }
            _reigniteFlash *= 0.92f;

            // 自转: 激活态更快
            _spin += 0.010f + _activeGlow * 0.014f + _reigniteFlash * 0.05f;

            if (!Main.dedServ) {
                Lighting.AddLight(Projectile.Center, GoldPrimary.ToVector3() * (0.35f + 0.3f * _activeGlow) * _unfold * (1f - _collapse));

                // 环沿零星金尘 (克制)
                if (_unfold >= 1f && _collapse <= 0f && Main.rand.NextBool(6)) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + a.ToRotationVector2() * WorldRadius * 0.94f,
                        DustID.GoldCoin, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 120, default, 0.9f);
                    d.noGravity = true;
                }
            }
        }

        /// <summary>easeOutBack: 展开时轻微过冲回弹 (弹性生长)。</summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || _unfold <= 0.02f)
                return false;

            Effect fx = WeaponVFX.GetEffect("BaGuaArray");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return false;

            float fade = 1f - _collapse;
            if (fade <= 0.02f)
                return false;

            float grow = EaseOutBack(_unfold) * MathHelper.Lerp(1f, 0.6f, _collapse); // 收拢时向内缩
            float pulse = 1f + 0.025f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f);
            // 外环位于 quad 半宽的 0.94 → 由世界半径反推 quad 缩放
            float scale = WorldRadius / 0.94f * 2f / noise.Width * grow * pulse;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue((0.55f + 0.25f * _reigniteFlash) * fade);
            fx.Parameters["uProgress"]?.SetValue(_unfold);
            fx.Parameters["uActive"]?.SetValue(MathHelper.Clamp(_activeGlow + _reigniteFlash * 0.8f, 0f, 1f));
            fx.Parameters["uSpin"]?.SetValue(_spin);
            fx.Parameters["uColorPrimary"]?.SetValue(GoldPrimary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(AzureSecondary.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(noise, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, noise.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);

            // 完成/重燃闪光: 短促外扩环
            if (_reigniteFlash > 0.15f && _collapse <= 0f)
                WeaponVFX.DrawShockwaveRing(Projectile.Center, WorldRadius * (1.35f - _reigniteFlash * 0.35f),
                    9f, _reigniteFlash * 0.7f, GoldPrimary, AzureSecondary);
            if (_unfold < 1f) {
                // 展开期: 从中心外扩的引导环
                WeaponVFX.DrawShockwaveRing(Projectile.Center, WorldRadius * _unfold, 7f,
                    (1f - _unfold) * 0.55f + 0.15f, GoldPrimary, AzureSecondary);
            }

            return false;
        }
    }
}

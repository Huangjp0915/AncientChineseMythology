using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 宝石棍左键: 横扫 → 回扫 → 重抡; 每次命中沿击飞方向绽出 2 枚棱光碎片 (六色轮转, 0.25x)。
    /// hue 流转拖尾 — "碎片颜色即判定"的多彩语言。
    /// </summary>
    internal class GemStickSpearProjectile : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GemStickSpearProjectile";

        /// <summary>六色宝石轮盘 (红/绿/蓝/金/紫/钻白) — 系列内共享。</summary>
        public static readonly Color[] GemColors = {
            new(255, 80, 90), new(110, 240, 130), new(90, 150, 255),
            new(255, 210, 90), new(190, 110, 255), new(235, 245, 255),
        };

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.7f, 1f),
            SwingStep.Sweep(3.7f, 1.05f, sign: -1),
            SwingStep.Sweep(4.5f, 1.3f, sign: 1, timeMul: 1.25f, scaleMul: 1.1f, impact: true),
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 30;
        protected override Color TrailOuter {
            get {
                float hue = (Main.GlobalTimeWrappedHourly * 0.25f) % 1f;
                Color c = Main.hslToRgb(hue, 1f, 0.35f);
                c.A = 150;
                return c;
            }
        }
        protected override Color TrailInner {
            get {
                float hue = (Main.GlobalTimeWrappedHourly * 0.25f + 0.12f) % 1f;
                Color c = Main.hslToRgb(hue, 1f, 0.72f);
                c.A = 205;
                return c;
            }
        }
        protected override float TipLength => 98f;
        protected override float Overshoot => 0.14f;
        protected override int BurstTheme => ACMWeaponBurst.Gem;
        protected override float HitShake => 2f;
        protected override int HitDustType => DustID.GemDiamond;
        protected override Vector3 GlowLight => new(0.35f, 0.25f, 0.5f);

        protected override void OnStickHitNPC(NPC target, NPC.HitInfo hit) {
            // 命中绽碎片: owner 端生成 (同屏上限 12), 沿击飞方向扇形
            if (Main.myPlayer != Projectile.owner)
                return;
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<GemShardProj>()] >= 12)
                return;

            Vector2 away = (target.Center - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = away.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(7f, 10.5f);
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, vel,
                    ModContent.ProjectileType<GemShardProj>(), (int)(Projectile.damage * 0.25f), 1f,
                    Projectile.owner, Main.rand.Next(GemColors.Length));
            }
        }
    }

    /// <summary>
    /// 棱光碎片: 宝石棍命中绽出/棱光回旋甩出的小穿刺体。颜色由 ai[0] 指定 (生成包同步)。
    /// 无独立贴图 — Sparkle 遮罩 + 细拖尾程序化绘制。
    /// </summary>
    internal class GemShardProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override LocalizedText DisplayName
            => Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.GemStickSpearProjectile.DisplayName");

        private Color ShardColor => GemStickSpearProjectile.GemColors[
            Math.Clamp((int)Projectile.ai[0], 0, GemStickSpearProjectile.GemColors.Length - 1)];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, ShardColor.ToVector3() * 0.3f);
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GemDiamond,
                    Projectile.velocity * 0.1f, 120, ShardColor, 0.8f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GemDiamond,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 100, ShardColor, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Color c = ShardColor;
            c.A = 150;
            Color inner = Color.Lerp(ShardColor, Color.White, 0.6f);
            inner.A = 205;
            WeaponVFX.DrawProjectileTrail(Projectile, 4f, c, inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f, subdivisions: 2);

            Texture2D star = ACMAsset.Sparkle;
            if (star != null) {
                Color glow = ShardColor * Projectile.Opacity;
                glow.A = 0;
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(star, Projectile.Center - Main.screenPosition, null, glow,
                    Projectile.rotation + Main.GlobalTimeWrappedHourly * 4f, star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
                sb.Draw(star, Projectile.Center - Main.screenPosition, null, glow * 0.7f,
                    -Projectile.rotation, star.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }
            return false;
        }
    }
}

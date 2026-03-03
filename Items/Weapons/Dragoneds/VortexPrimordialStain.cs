using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 漩流玄印权杖 —— 超级毕业法杖，珊瑚海洋主题，发射旋转漩流光球追踪追撜敌人
    /// </summary>
    public class VortexPrimordialStain : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 475;
            Item.DamageType = DamageClass.Magic;
            Item.width  = 50;
            Item.height = 50;
            Item.useTime      = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10;
            Item.crit  = 24;
            Item.mana  = 30;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.noMelee      = true;
            Item.shoot = ModContent.ProjectileType<VortexStainOrb>();
            Item.shootSpeed = 18f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item8, player.position);
            // 发射 3 枚旋转漩流球，小角度散射
            for (int i = -1; i <= 1; i++) {
                float spread = MathHelper.ToRadians(i * 4.5f);
                Projectile.NewProjectile(source, position, velocity.RotatedBy(spread),
                    type, damage, knockback, player.whoAmI, ai0: i * MathHelper.PiOver2);
            }
            return false;
        }
    }

    public class VortexStainOrb : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/BlankStar";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        private bool _homing = false;
        private float _life = 0f;
        private const float HOME_START = 60f;

        public override void SetDefaults() {
            Projectile.width  = 30;
            Projectile.height = 30;
            Projectile.friendly    = true;
            Projectile.tileCollide = false;
            Projectile.penetrate   = 6;
            Projectile.timeLeft    = 240;
            Projectile.DamageType  = DamageClass.Magic;
            Projectile.light       = 0.8f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 10;
        }

        public override void AI() {
            _life++;
            Projectile.rotation += 0.20f;

            if (!_homing && _life >= HOME_START) _homing = true;

            if (_homing) {
                NPC target = null;
                float best = 850f;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC n = Main.npc[i];
                    if (!n.active || n.friendly || n.dontTakeDamage) continue;
                    float d = Vector2.Distance(Projectile.Center, n.Center);
                    if (d < best) { best = d; target = n; }
                }
                if (target != null) {
                    Vector2 dir = Projectile.DirectionTo(target.Center);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 26f, 0.12f);
                }
            }
            else {
                // 前期轻微旋转飞行路径
                float angle = Projectile.ai[0] + _life * 0.08f;
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Projectile.velocity += perp * MathF.Sin(angle) * 0.4f;
                if (Projectile.velocity.Length() > 20f)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D star = ACMAsset.BlankStar;
            Texture2D sg   = ACMAsset.SoftGlow;
            Texture2D arc  = ACMAsset.ElectricArcSheet;

            float pulse = 0.80f + 0.20f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);

            // ── 拖尾光带 ──
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.58f;
                sb.Draw(sg,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(0, 210, 185) * a, 0f,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                    0.70f, SpriteEffects.None, 0);
            }

            // ── 电弧光环 ──
            int row = (int)(Main.timeForVisualEffects / 6) % 4;
            Rectangle arcFrame = new Rectangle(0, row * (arc.Height / 4), arc.Width, arc.Height / 4);
            sb.Draw(arc, Projectile.Center - Main.screenPosition, arcFrame,
                new Color(0, 200, 170) * (0.55f * pulse), Projectile.rotation,
                new Vector2(arcFrame.Width * 0.5f, arcFrame.Height * 0.5f),
                0.85f, SpriteEffects.None, 0);
            sb.Draw(arc, Projectile.Center - Main.screenPosition, arcFrame,
                new Color(255, 100, 140) * (0.38f * pulse), Projectile.rotation + MathHelper.PiOver4,
                new Vector2(arcFrame.Width * 0.5f, arcFrame.Height * 0.5f),
                0.62f, SpriteEffects.None, 0);

            // ── BlankStar 主体 ──
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(0, 230, 200) * (1.0f * pulse), Projectile.rotation,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                1.60f, SpriteEffects.None, 0);
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(255, 110, 145) * (0.48f * pulse), Projectile.rotation + MathHelper.PiOver4,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                1.05f, SpriteEffects.None, 0);

            // ── SoftGlow 核心 ──
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(80, 255, 225) * (0.88f * pulse), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                1.05f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

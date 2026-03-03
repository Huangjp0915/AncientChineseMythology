using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 银脉湮灭冲锋枪 —— 超级毕业枪械，银白科技主题，高速连射银蓝能量脉冲弹，
    /// 每隔8发自动触发一次超载连射（同帧发射3枚扇形弹），命中产生精准电弧碎花
    /// </summary>
    public class ArgentPulseObliterator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 370;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 70;
            Item.height = 28;
            Item.useTime = 7;
            Item.useAnimation = 7;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4;
            Item.crit = 16;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.notAmmo = true;
            Item.shoot = ModContent.ProjectileType<ArgentPulseBolt>();
            Item.shootSpeed = 22f;
        }

        // ShotCounter 存在 Item.stack 用不到的字段不合适，改用玩家 ai 也不安全——
        // 直接用静态计数即可（单客户端安全）
        private int _shotCount;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
                                   Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item11, player.position);
            _shotCount++;

            if (_shotCount >= 8) {
                // 超载：3枚扇形弹（-4° / 0° / +4°）
                _shotCount = 0;
                SoundEngine.PlaySound(SoundID.Item92, player.position);
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(4f, 8);
                float baseAngle = velocity.ToRotation();
                for (int k = -1; k <= 1; k++) {
                    Vector2 v = (baseAngle + k * MathHelper.ToRadians(4f)).ToRotationVector2() * velocity.Length();
                    var p = Projectile.NewProjectile(source, position, v, type, (int)(damage * 1.35f), knockback, player.whoAmI);
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].ai[1] = 1f; // 标记为超载弹
                }
            }
            else {
                // 普通：双发轻微散射（±1.8°）
                for (int k = -1; k <= 1; k += 2) {
                    Vector2 v = velocity.RotatedBy(k * MathHelper.ToRadians(1.8f));
                    Projectile.NewProjectile(source, position, v, type, damage, knockback, player.whoAmI);
                }
            }
            return false;
        }
    }

    public class ArgentPulseBolt : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        // ai[1] == 1f → 超载弹（更大更亮）
        private bool IsOvercharge => Projectile.ai[1] > 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.0f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
        }

        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
            int impType = ModContent.ProjectileType<ArgentPulseImpact>();
            var p = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center,
                Vector2.Zero, impType, 0, 0f, Projectile.owner);
            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].ai[1] = IsOvercharge ? 1f : 0f;
        }

        public override bool PreDraw(ref Color lightColor) {
            bool oc = IsOvercharge;
            float ow = oc ? 1.35f : 1.0f; // 超载弹尺寸倍率
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D arc = ACMAsset.ElectricArcSheet;

            int len = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = 1; i < len; i++) {
                float t = 1f - i / (float)len;
                float a = t * 0.82f;
                // 外层银白宽拖尾
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(200, 215, 255) * a,
                    Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2((0.62f + i * 0.016f) * ow, 0.19f * ow), SpriteEffects.None, 0);
                // 内层电弧青蓝细核
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(60, 200, 255) * (a * 0.48f),
                    Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.26f * ow, 0.08f * ow), SpriteEffects.None, 0);
            }

            // 弹头主体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                oc ? new Color(180, 240, 255) : new Color(220, 235, 255),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(1.10f * ow, 0.28f * ow), SpriteEffects.None, 0);

            // 弹头柔光
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(140, 210, 255) * 0.90f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.55f * ow, SpriteEffects.None, 0);

            // 超载弹附加 ElectricArcSheet 小电光环
            if (oc) {
                int arcFrame = (int)(Main.timeForVisualEffects / 2) % 4;
                Rectangle arcSrc = new Rectangle(0, arcFrame * (arc.Height / 4), arc.Width, arc.Height / 4);
                sb.Draw(arc, Projectile.Center - Main.screenPosition, arcSrc,
                    new Color(100, 220, 255) * 0.60f,
                    Projectile.rotation,
                    new Vector2(arc.Width * 0.5f, (arc.Height / 4) * 0.5f),
                    0.22f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class ArgentPulseImpact : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/ArgentPulseObliterator";

        private bool IsOvercharge => Projectile.ai[1] > 0.5f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 28;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog  = 1f - Projectile.timeLeft / 28f;
            float alpha = MathHelper.SmoothStep(0.95f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, IsOvercharge ? 9f : 5.5f, ACMUtils.QuadOut(prog));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D light = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D arc = ACMAsset.ElectricArcSheet;

            // 4道精准银白细刺（水平+垂直）
            for (int k = 0; k < 4; k++) {
                sb.Draw(light, Projectile.Center - Main.screenPosition, null,
                    new Color(200, 220, 255) * (alpha * 0.85f),
                    k * MathHelper.PiOver2,
                    new Vector2(light.Width * 0.5f, light.Height),
                    new Vector2(0.12f, scale * 0.55f), SpriteEffects.None, 0);
            }

            // ElectricArcSheet 能量放电环
            int arcFrame = (int)(Main.timeForVisualEffects / 2) % 4;
            Rectangle arcSrc = new Rectangle(0, arcFrame * (arc.Height / 4), arc.Width, arc.Height / 4);
            sb.Draw(arc, Projectile.Center - Main.screenPosition, arcSrc,
                new Color(80, 200, 255) * (alpha * 0.70f),
                (float)Main.timeForVisualEffects * 0.04f,
                new Vector2(arc.Width * 0.5f, (arc.Height / 4) * 0.5f),
                scale * 0.38f, SpriteEffects.None, 0);

            // 超载弹附加斜向4细刺
            if (IsOvercharge) {
                for (int k = 0; k < 4; k++) {
                    sb.Draw(light, Projectile.Center - Main.screenPosition, null,
                        new Color(120, 235, 255) * (alpha * 0.65f),
                        k * MathHelper.PiOver2 + MathHelper.PiOver4,
                        new Vector2(light.Width * 0.5f, light.Height),
                        new Vector2(0.10f, scale * 0.38f), SpriteEffects.None, 0);
                }
            }

            // 白核闪光（前段高亮后段淡出）
            float flashA = MathHelper.SmoothStep(1.1f, 0f, prog * 1.8f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(240, 250, 255) * (alpha * flashA), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.20f, SpriteEffects.None, 0);
            // 青蓝扩散光晕
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(80, 190, 255) * (alpha * 0.45f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.50f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

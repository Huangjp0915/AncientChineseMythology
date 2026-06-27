using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 天律宝典 - 天柱敌怪掉落的魔法书类武器
    /// 金色+青色主题，释放天律符文打击敌人
    /// </summary>
    public class TomeofDivineLaw : ModItem
    {
        private int castCount = 0;

        public override void SetDefaults() {
            Item.damage = 155;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item103;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DivineRune>();
            Item.shootSpeed = 12f;
            Item.mana = 12;
            Item.crit = 10;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            castCount++;

            // 普通释放：符文弹幕
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            // 每四次施法释放符文阵
            if (castCount >= 4) {
                castCount = 0;
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f, Volume = 0.8f }, player.Center);

                // 在鼠标位置释放符文阵
                Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero,
                    ModContent.ProjectileType<RuneCircle>(), (int)(damage * 1.5f), knockback, player.whoAmI);

                // 释放粒子
                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20;
                    Vector2 vel = angle.ToRotationVector2() * 5f;
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                    int dust = Dust.NewDust(Main.MouseWorld, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 释放粒子
            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(2, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(position, 0, 0, dustType, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "记载天界法则的神圣典籍"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "发射天律符文，每四次施法释放符文阵"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 天律符文 - 追踪符文弹幕
    /// </summary>
    public class DivineRune : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BookStaffShot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI() {
            Projectile.rotation += 0.12f;

            // 追踪
            NPC target = FindClosestNPC(500f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.05f);
            }

            // 金色+青色粒子
            int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
            int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.4f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = -Projectile.velocity * 0.08f;

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.5f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;
            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(3, 8);
            Vector2 origin = rectangle.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Gold * 0.4f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 符文阵 - 范围伤害
    /// </summary>
    public class RuneCircle : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LunarFlare;

        private float circleRadius = 0f;
        private float circleAlpha = 0f;
        private const float MaxRadius = 120f;

        public override void SetDefaults() {
            Projectile.width = 240;
            Projectile.height = 240;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation += 0.08f;

            // 扩张
            if (Projectile.timeLeft > 30) {
                circleRadius = MathHelper.Lerp(circleRadius, MaxRadius, 0.1f);
                circleAlpha = MathHelper.Lerp(circleAlpha, 1f, 0.1f);
            }
            else {
                circleAlpha = Projectile.timeLeft / 30f;
            }

            // 符文阵粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * circleRadius;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
            }

            // 内圈粒子
            for (int i = 0; i < 3; i++) {
                float angle = Projectile.rotation + MathHelper.TwoPi * i / 6;
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * (circleRadius * 0.5f);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * circleAlpha * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            return Vector2.Distance(Projectile.Center, targetCenter) < circleRadius + 30f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 符文阵核心金白径向辉光
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.1f, 0.4f * circleAlpha, new Color(255, 245, 200), 6f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 绘制符文阵 - 多层旋转
            int runeCount = 6;
            for (int layer = 0; layer < 3; layer++) {
                float layerRadius = circleRadius * (0.4f + layer * 0.3f);
                float layerRot = Projectile.rotation * (1f + layer * 0.3f) * (layer % 2 == 0 ? 1 : -1);

                Color layerColor = layer switch {
                    0 => Color.Gold,
                    1 => new Color(100, 200, 180),
                    _ => Color.White
                };
                layerColor *= circleAlpha * (0.6f - layer * 0.15f);

                for (int i = 0; i < runeCount; i++) {
                    float angle = layerRot + MathHelper.TwoPi * i / runeCount;
                    Vector2 runePos = screenPos + angle.ToRotationVector2() * layerRadius;

                    Main.spriteBatch.Draw(tex, runePos, null, layerColor, angle + Projectile.rotation, origin, 0.4f - layer * 0.1f, SpriteEffects.None, 0f);
                }
            }

            // 中心光芒
            Color centerColor = Color.Gold * circleAlpha * 0.5f;
            Main.spriteBatch.Draw(tex, screenPos, null, centerColor, Projectile.rotation * 2f, origin, 0.8f * circleAlpha, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消散爆发
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

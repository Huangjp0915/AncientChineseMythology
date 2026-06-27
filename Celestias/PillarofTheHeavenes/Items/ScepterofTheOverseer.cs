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
    /// 监天权杖 - 天柱敌怪掉落的法杖类魔法武器
    /// 金色+青色主题，发射追踪天光球
    /// </summary>
    public class ScepterofTheOverseer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 165;
            Item.DamageType = DamageClass.Magic;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<OverseerOrb>();
            Item.shootSpeed = 10f;
            Item.mana = 14;
            Item.crit = 8;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 发射两个光球
            for (int i = -1; i <= 1; i += 2) {
                Vector2 offset = velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * i * 15f;
                Vector2 newVel = velocity.RotatedBy(MathHelper.ToRadians(8 * i));
                Projectile.NewProjectile(source, position + offset, newVel, type, damage, knockback, player.whoAmI);
            }

            // 释放粒子
            for (int i = 0; i < 12; i++) {
                Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 7);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(position, 0, 0, dustType, dustVel.X, dustVel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "监视天界的神圣权杖"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "发射追踪天光球，命中后爆炸"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 监天光球 - 追踪并爆炸
    /// </summary>
    public class OverseerOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LunarFlare;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Projectile.rotation += 0.15f;

            // 追踪
            NPC target = FindClosestNPC(600f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.06f);
            }

            // 金色+青色粒子
            for (int i = 0; i < 2; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(8, 8), 0, 0, dustType, 0, 0, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.6f);
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
            // 爆炸
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);

            // 大量金色粒子爆发
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // 青色粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.IceTorch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.2f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(2, 5);
            Vector2 origin = rectangle.Size() / 2f;

            // 金色拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerGlow = Color.Gold * 0.4f;
            outerGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, outerGlow, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0f);

            // 中层青色
            Color midGlow = new Color(100, 200, 180) * 0.5f;
            midGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, midGlow, -Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0f);

            // 核心
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale * 0.8f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

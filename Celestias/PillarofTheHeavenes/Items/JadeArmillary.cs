using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 玉衡浑仪 - 天柱敌怪掉落的回旋镖类武器
    /// 金色+青色主题，像浑天仪般旋转飞行
    /// </summary>
    public class JadeArmillary : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 195;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<JadeArmillaryProjectile>();
            Item.shootSpeed = 16f;
            Item.crit = 10;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<JadeArmillaryProjectile>()] < 2;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "仿天柱浑天仪铸造的神器"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "旋转飞行，命中后释放星辰碎片"));
        }
    }

    /// <summary>
    /// 玉衡浑仪弹幕
    /// </summary>
    public class JadeArmillaryProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/JadeArmillary";

        private float spinSpeed = 0.4f;
        private bool returning = false;
        private int hitCounter = 0;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 35;
            Projectile.height = 35;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation += spinSpeed;

            if (!returning) {
                Projectile.velocity *= 0.97f;
                if (Projectile.velocity.Length() < 4f || Projectile.timeLeft < 240) {
                    returning = true;
                }
            }
            else {
                // 返回玩家
                Vector2 toOwner = Owner.Center - Projectile.Center;
                float distance = toOwner.Length();

                if (distance < 30f) {
                    Projectile.Kill();
                    return;
                }

                float returnSpeed = 18f + (300 - Projectile.timeLeft) * 0.15f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.12f);
            }

            // 金色+青色旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = Projectile.rotation + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * 18f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hitCounter++;

            // 金色爆发
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 每命中3次释放星辰碎片
            if (hitCounter >= 3) {
                hitCounter = 0;
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);

                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4 + Projectile.rotation;
                    Vector2 vel = angle.ToRotationVector2() * 8f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        ModContent.ProjectileType<CelestialFragment>(), Projectile.damage / 2, 2f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 旋转拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层光环
            Color outerGlow = Color.Gold * 0.4f;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, outerGlow, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            // 主体
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 星辰碎片 - 浑仪释放的小型追踪弹
    /// </summary>
    public class CelestialFragment : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/JadeArmillary";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI() {
            Projectile.rotation += 0.2f;

            // 追踪
            NPC target = FindClosestNPC(400f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 12f, 0.08f);
            }

            // 金色粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.4f) * 0.4f);
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

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Gold * progress * 0.5f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, 0.5f * progress, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.Gold, Projectile.rotation, origin, 0.6f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

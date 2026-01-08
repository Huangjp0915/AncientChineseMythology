using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs.Items
{
    /// <summary>
    /// 漩涡龙弓 - 敖广掉落的弓类远程武器
    /// 将箭矢转化为水箭，蓄力可发射巨型水龙卷箭
    /// </summary>
    public class MaelstromBow : ModItem
    {
        private int chargeTime = 0;
        private const int MaxCharge = 60;

        public override void SetDefaults() {
            Item.damage = 280;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 30;
            Item.height = 60;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 14;
            Item.channel = true;
        }

        public override void HoldItem(Player player) {
            if (player.channel && player.HasAmmo(Item)) {
                // 满蓄力提示
                if (chargeTime == MaxCharge - 1) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.3f, Volume = 0.6f }, player.Center);
                }
                chargeTime++;
                if (chargeTime > MaxCharge) chargeTime = MaxCharge;

                // 蓄力粒子
                if (chargeTime > 20 && Main.rand.NextBool(3)) {
                    float progress = (float)chargeTime / MaxCharge;
                    Vector2 dustPos = player.Center + Main.rand.NextVector2CircularEdge(60 * progress, 60 * progress);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.5f * progress);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }
            else if (chargeTime > 0) {
                // 释放蓄力射击
                if (chargeTime >= MaxCharge) {
                    ShootChargedArrow(player);
                }
                chargeTime = 0;
            }
        }

        private void ShootChargedArrow(Player player) {
            if (!player.HasAmmo(Item)) return;

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 发射龙卷箭
            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                player.Center,
                direction * 20f,
                ModContent.ProjectileType<TornadoArrow>(),
                Item.damage * 3,
                Item.knockBack * 2f,
                player.whoAmI
            );

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1f }, player.Center);
            player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 15);

            // 消耗箭矢
            player.PickAmmo(Item, out _, out _, out _, out _, out _, true);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // 将箭矢转化为水箭
            type = ModContent.ProjectileType<DragonWaterArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 普通射击：发射3发水箭
            for (int i = -1; i <= 1; i++) {
                Vector2 newVel = velocity.RotatedBy(MathHelper.ToRadians(5 * i));
                Projectile.NewProjectile(source, position, newVel, type, damage, knockback, player.whoAmI);
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "以龙筋为弦的海龙神弓"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "将箭矢转化为追踪水箭"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "长按蓄力释放巨型水龙卷箭"));
        }
    }

    /// <summary>
    /// 水龙箭
    /// </summary>
    public class DragonWaterArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float arrowPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
        }

        public override void AI() {
            arrowPhase += 0.1f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微追踪
            if (Projectile.timeLeft > 120) {
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.03f);
                }
            }

            // 水流粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.4f);
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
            target.AddBuff(BuffID.Slow, 60);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0);

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                // LightShot朝右，箭矢旋转需要减去PiOver2
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2, origin, 
                    new Vector2(0.3f * progress, 0.5f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = AoGuangHelper.WaterGlow * 0.9f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor, 
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.4f, 0.6f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙卷箭 - 蓄力发射的强力箭矢
    /// </summary>
    public class TornadoArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tornadoPhase;
        private float tornadoScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            tornadoPhase += 0.2f;
            tornadoScale = MathHelper.Lerp(tornadoScale, 1.5f, 0.05f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙卷风效果 - 复用BarrierWaterTornado的视觉
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    float angle = tornadoPhase + MathHelper.TwoPi * i / 6;
                    float radius = 20f * tornadoScale;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f * tornadoScale);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            // 吸引附近敌人
            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < 150f && distance > 30f) {
                    Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += pullDir * 0.5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * tornadoScale);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 180);

            // 龙卷击中效果
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15 + tornadoPhase;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.2f, Volume = 0.7f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 使用原版龙卷风纹理
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 拖尾龙卷
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailRot = tornadoPhase - i * 0.15f;

                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(tornadoTex, pos, null, trailColor, trailRot, origin, 0.4f * progress * tornadoScale, SpriteEffects.None, 0f);
            }

            // 主体龙卷
            for (int layer = 2; layer >= 0; layer--) {
                float layerRot = tornadoPhase * (1f + layer * 0.3f) * (layer % 2 == 0 ? 1 : -1);
                float layerScale = (0.4f + layer * 0.15f) * tornadoScale;

                Color layerColor = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    _ => AoGuangHelper.OceanTeal
                };
                layerColor *= 0.7f - layer * 0.15f;
                layerColor.A = 0;

                sb.Draw(tornadoTex, screenPos, null, layerColor, layerRot, origin, layerScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);

            // 爆发水花
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5, 10);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Water,
                    1 => DustID.BlueTorch,
                    _ => DustID.Wet
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 15);
        }
    }
}

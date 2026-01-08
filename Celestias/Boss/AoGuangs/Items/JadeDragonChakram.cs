using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs.Items
{
    /// <summary>
    /// 玉龙环刃 - 敖广掉落的回旋镖类武器
    /// 投掷后旋转飞行并吸引周围水流，返回时造成更高伤害
    /// </summary>
    public class JadeDragonChakram : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Melee;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<JadeDragonChakramProjectile>();
            Item.shootSpeed = 18f;
            Item.crit = 10;
        }

        public override bool CanUseItem(Player player) {
            // 限制同时存在的回旋镖数量
            return player.ownedProjectileCounts[ModContent.ProjectileType<JadeDragonChakramProjectile>()] < 2;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "以龙宫玉石雕琢的水龙环刃"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "飞行时吸引周围水流形成漩涡"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "返回时伤害提升50%"));
        }
    }

    /// <summary>
    /// 玉龙环刃弹幕
    /// </summary>
    public class JadeDragonChakramProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float rotationSpeed = 0.3f;
        private bool isReturning = false;
        private float vortexPhase = 0f;
        private int hitCount = 0;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 45;
            Projectile.height = 45;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            vortexPhase += 0.15f;
            Projectile.rotation += rotationSpeed;

            // 飞行逻辑
            if (!isReturning) {
                Projectile.velocity *= 0.97f;

                // 速度过低时开始返回
                if (Projectile.velocity.Length() < 4f || Projectile.timeLeft < 240) {
                    isReturning = true;
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

                // 追踪返回
                float returnSpeed = 20f + (300 - Projectile.timeLeft) * 0.1f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.1f);
            }

            // 水漩涡效果
            DrawVortexDust();

            // 吸引附近敌人的弹幕效果（视觉）
            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.7f);
        }

        private void DrawVortexDust() {
            if (Main.netMode == NetmodeID.Server) return;

            // 绘制漩涡粒子
            int ringCount = isReturning ? 3 : 2;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = 30f + ring * 15f;
                float ringRot = vortexPhase * (1f - ring * 0.2f) * (ring % 2 == 0 ? 1 : -1);

                if (Main.rand.NextBool(2)) {
                    float angle = ringRot + Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 返回时伤害提升
            if (isReturning) {
                modifiers.SourceDamage *= 1.5f;
            }

            hitCount++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 90);

            // 水花效果
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 多次命中后释放水波
            if (hitCount >= 5) {
                hitCount = 0;
                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);

                // 释放水波
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6;
                    Vector2 vel = angle.ToRotationVector2() * 8f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        ModContent.ProjectileType<ChakramWaterBurst>(), Projectile.damage / 2, 2f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<JadeDragonChakram>()].Value;
            Vector2 origin = tex.Size() / 2f;

            float pulse = 1f + MathF.Sin(vortexPhase * 2f) * 0.1f;

            // 漩涡拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                // 螺旋偏移
                float spiralAngle = vortexPhase - i * 0.3f;
                Vector2 spiralOffset = spiralAngle.ToRotationVector2() * (10f * progress);

                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f + spiralOffset - Main.screenPosition;
                Main.spriteBatch.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, 0.6f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 外层漩涡
            Color outerColor = AoGuangHelper.OceanTeal * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, outerColor,
                Projectile.rotation * 0.5f, origin, 1.2f * pulse, SpriteEffects.None, 0f);

            // 主体
            Color mainColor = AoGuangHelper.WaterGlow * 0.8f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, 0.8f * pulse, SpriteEffects.None, 0f);

            // 内核
            Color coreColor = AoGuangHelper.PureWhite * 0.6f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, coreColor,
                -Projectile.rotation, origin, 0.4f * pulse, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                -Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 环刃水波爆发
    /// </summary>
    public class ChakramWaterBurst : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.96f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoGuangHelper.OceanTeal * progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.4f * progress, SpriteEffects.None, 0f);
            }

            Color mainColor = AoGuangHelper.WaterGlow * 0.7f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor, 0f, origin, 0.5f, SpriteEffects.None, 0f);

            return false;
        }
    }
}

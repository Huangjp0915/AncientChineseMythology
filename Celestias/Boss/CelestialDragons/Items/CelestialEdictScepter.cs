using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons.Items
{
    /// <summary>
    /// 敕令 - 天庭巡卫金龙掉落的法师武器
    /// 由金龙天庭权柄凝聚而成的神杖，蕴含天庭法令之力
    /// 特效：释放金色敕令符咒，追踪敌人并引发天雷；蓄力可召唤龙威法阵，降下天庭审判
    /// </summary>
    public class CelestialEdictScepter : ModItem
    {
        private int chargeTime = 0;
        private const int MaxCharge = 60;
        private bool isFullyCharged = false;

        public override void SetDefaults() {
            Item.damage = 4120;
            Item.DamageType = DamageClass.Magic;
            Item.width = 25;
            Item.height = 25;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(gold: 35);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<CelestialEdictSeal>();
            Item.shootSpeed = 12f;
            Item.mana = 20;
            Item.staff[Item.type] = false;
            Item.crit = 12;
            Item.channel = true;
        }

        public override void HoldItem(Player player) {
            if (player.channel && player.CheckMana(Item, -1, false, false)) {
                chargeTime++;

                // 蓄力粒子效果
                if (chargeTime > 15 && Main.rand.NextBool(2)) {
                    float chargeProgress = Math.Min((chargeTime - 15) / (float)(MaxCharge - 15), 1f);
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(60 * (1 - chargeProgress), 60 * (1 - chargeProgress));
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f * chargeProgress);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                // 蓄力光环
                if (chargeTime > 20) {
                    float chargeProgress = Math.Min((chargeTime - 20) / (float)(MaxCharge - 20), 1f);
                    Lighting.AddLight(player.Center, new Vector3(1f, 0.85f, 0.3f) * chargeProgress);
                }

                // 满蓄力提示
                if (chargeTime == MaxCharge) {
                    isFullyCharged = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.9f }, player.Center);

                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi * i / 16f;
                        Vector2 vel = angle.ToRotationVector2() * 6f;
                        int dust = Dust.NewDust(player.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }

                if (chargeTime > MaxCharge) chargeTime = MaxCharge;
            }
            else if (chargeTime > 0 && !player.channel) {
                // 释放蓄力
                if (isFullyCharged) {
                    CastCelestialJudgment(player);
                }
                else if (chargeTime > 8) {
                    CastEdictSeals(player);
                }
                chargeTime = 0;
                isFullyCharged = false;
            }
        }

        private void CastEdictSeals(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (!player.CheckMana(Item, -1, true)) return;

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 释放3个敕令符咒
            for (int i = -1; i <= 1; i++) {
                Vector2 spawnOffset = direction.RotatedBy(MathHelper.PiOver2) * i * 30f;
                Vector2 vel = direction.RotatedBy(MathHelper.ToRadians(8 * i)) * Item.shootSpeed;
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center + spawnOffset, vel,
                    ModContent.ProjectileType<CelestialEdictSeal>(), Item.damage, Item.knockBack, player.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f }, player.Center);
        }

        private void CastCelestialJudgment(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (!player.CheckMana(Item, 50, true)) return;

            Vector2 targetPos = Main.MouseWorld;

            // 召唤龙威法阵
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), targetPos, Vector2.Zero,
                ModContent.ProjectileType<DragonAuthorityCircle>(), Item.damage * 2, Item.knockBack * 2f, player.whoAmI);

            // 四方敕令符咒
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * i;
                Vector2 sealPos = targetPos + angle.ToRotationVector2() * 200f;
                Vector2 vel = (targetPos - sealPos).SafeNormalize(Vector2.Zero) * 8f;
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), sealPos, vel,
                    ModContent.ProjectileType<CelestialEdictSeal>(), Item.damage, Item.knockBack, player.whoAmI, 1f);
            }

            SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.2f, Volume = 1.1f }, targetPos);
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.6f, Volume = 0.7f }, player.Center);

            // 屏幕震动
            if (player.whoAmI == Main.myPlayer) {
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 15);
            }

            // 施法特效
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dust = Dust.NewDust(player.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 使用自定义射击逻辑
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "EdictLore", "「天庭敕令，万物遵从」"));
            tooltips.Add(new TooltipLine(Mod, "EdictEffect", "释放金色敕令符咒，追踪敌人并引发天雷"));
            tooltips.Add(new TooltipLine(Mod, "EdictEffect2", "蓄力可召唤龙威法阵，降下天庭审判"));
            tooltips.Add(new TooltipLine(Mod, "EdictEffect3", "法阵内敌人受到持续伤害并被天雷轰击"));
        }
    }

    /// <summary>
    /// 敕令符咒 - 追踪敌人的金色符咒
    /// </summary>
    public class CelestialEdictSeal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float sealRotation = 0f;
        private bool isEmpowered => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            sealRotation += 0.15f;
            Projectile.rotation = sealRotation;

            // 追踪敌人
            NPC target = FindClosestNPC(isEmpowered ? 600f : 400f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float trackingSpeed = isEmpowered ? 0.08f : 0.05f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), trackingSpeed);
            }

            // 符咒粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(10, 10), 0, 0, DustID.GoldFlame, 0, 0, 100, default, isEmpowered ? 1.8f : 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            // 金色光芒
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * (isEmpowered ? 0.7f : 0.4f));
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage) {
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
            target.AddBuff(BuffID.Electrified, 180);

            // 引发天雷
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 lightningStart = target.Center + new Vector2(Main.rand.NextFloat(-50, 50), -600f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), lightningStart, new Vector2(0, 25f),
                    ModContent.ProjectileType<CelestialLightningBolt>(), Projectile.damage / 2, 0f, Projectile.owner, target.Center.X, target.Center.Y);
            }

            // 符咒命中效果
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GoldDragon, isEmpowered ? 1.2f : 0.9f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 敕令符咒金龙双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: isEmpowered ? 12f : 8f,
                outerColor: new Color(200, 130, 30, 120), innerColor: new Color(255, 240, 170, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D texture = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            float scale = isEmpowered ? 0.6f : 0.4f;

            // 符咒拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Gold * progress * 0.5f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, scale * progress, SpriteEffects.None, 0f);
            }

            // 符咒光晕
            Color glowColor = Color.Gold * 0.6f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, sealRotation, origin, scale * 1.3f, SpriteEffects.None, 0f);

            // 符咒主体
            Color mainColor = Color.White;
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, mainColor, sealRotation, origin, scale, SpriteEffects.None, 0f);

            // 内层符纹
            Color innerColor = Color.OrangeRed * 0.8f;
            innerColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, innerColor, -sealRotation * 0.5f, origin, scale * 0.5f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙威法阵 - 蓄力召唤的强力区域控制法阵
    /// </summary>
    public class DragonAuthorityCircle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float circleScale = 0f;
        private float runeRotation = 0f;
        private float pulsePhase = 0f;
        private int lightningTimer = 0;
        private int damageTimer = 0;

        public override void SetDefaults() {
            Projectile.width = 250;
            Projectile.height = 250;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            pulsePhase += 0.08f;
            runeRotation += 0.02f;
            damageTimer++;
            lightningTimer++;

            // 法阵展开
            if (Projectile.timeLeft > 270) {
                circleScale = MathHelper.Lerp(circleScale, 1.5f, 0.08f);
            }
            // 法阵消散
            else if (Projectile.timeLeft < 40) {
                circleScale = MathHelper.Lerp(circleScale, 0f, 0.06f);
            }

            // 调整碰撞范围
            int newSize = (int)(250 * circleScale);
            Projectile.width = Projectile.height = newSize;

            // 周期性天雷轰击
            if (lightningTimer >= 40 && circleScale > 0.8f) {
                lightningTimer = 0;
                SummonLightningStrike();
            }

            // 周期性对范围内敌人造成伤害
            if (damageTimer >= 15 && circleScale > 0.5f) {
                damageTimer = 0;
                DealCircleDamage();
            }

            // 法阵粒子效果
            CreateCircleParticles();

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * circleScale * 0.8f);
        }

        private void SummonLightningStrike() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 在法阵内随机位置降下天雷
            float radius = 100f * circleScale;
            Vector2 targetPos = Projectile.Center + Main.rand.NextVector2Circular(radius, radius);
            Vector2 lightningStart = targetPos + new Vector2(0, -700f);

            Projectile.NewProjectile(Projectile.GetSource_FromAI(), lightningStart, new Vector2(0, 30f),
                ModContent.ProjectileType<CelestialLightningBolt>(), Projectile.damage, 0f, Projectile.owner, targetPos.X, targetPos.Y);

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.6f }, targetPos);
        }

        private void DealCircleDamage() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float damageRadius = 120f * circleScale;

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage &&
                    Vector2.Distance(npc.Center, Projectile.Center) < damageRadius) {
                    npc.SimpleStrikeNPC(Projectile.damage / 3, 0, false, 0);
                    npc.AddBuff(BuffID.Electrified, 60);

                    // 受审判效果
                    for (int i = 0; i < 3; i++) {
                        int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.GoldFlame, 0, -2f, 100, default, 1.2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
        }

        private void CreateCircleParticles() {
            float effectiveRadius = 100f * circleScale;

            // 外圈旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;

                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 100, default, 1.5f * circleScale);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 4f;
            }

            // 符文闪烁
            if (Main.rand.NextBool(6)) {
                float runeAngle = runeRotation + MathHelper.TwoPi * Main.rand.Next(8) / 8f;
                Vector2 runePos = Projectile.Center + runeAngle.ToRotationVector2() * effectiveRadius * 0.7f;

                int dust = Dust.NewDust(runePos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            // 中心能量涌动
            if (Main.rand.NextBool(4)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(20 * circleScale, 20 * circleScale);
                int dust = Dust.NewDust(pos, 0, 0, DustID.GoldFlame, 0, -3f, 100, default, 2f * circleScale);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool? CanHitNPC(NPC target) => false; // 使用自定义伤害

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D starTex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 starOrigin = starTex.Size() / 2f;
            float effectiveRadius = 100f * circleScale;

            // 龙威法阵核心金芒径向辉光 (天庭审判定调)
            if (circleScale > 0.5f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.18f * circleScale, 0.45f * circleScale,
                    new Color(255, 205, 90), 12f);

            // 多层法阵环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = effectiveRadius * (0.5f + ring * 0.25f);
                float ringRotation = runeRotation * (ring % 2 == 0 ? 1 : -1.5f);
                int segments = 12 - ring * 2;
                float ringAlpha = (0.7f - ring * 0.15f) * circleScale;

                // 符文节点
                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    float pulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                    Color runeColor = Color.Lerp(Color.Gold, Color.OrangeRed, pulse) * ringAlpha;
                    runeColor.A = 0;

                    float runeScale = (0.3f + pulse * 0.2f) * circleScale;
                    sb.Draw(starTex, pos - Main.screenPosition, null, runeColor, angle + MathHelper.PiOver4, starOrigin, runeScale, SpriteEffects.None, 0f);
                }
            }

            // 龙形中心图腾
            DrawDragonTotem(sb, effectiveRadius);

            // 外圈光环
            int borderSegments = 48;
            for (int i = 0; i < borderSegments; i++) {
                float angle = MathHelper.TwoPi * i / borderSegments;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;

                float pulse = MathF.Sin(pulsePhase * 2f + angle * 6f) * 0.3f + 0.7f;
                Color borderColor = Color.Gold * pulse * circleScale * 0.6f;
                borderColor.A = 0;

                sb.Draw(starTex, pos - Main.screenPosition, null, borderColor, angle, starOrigin, 0.2f * circleScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        private void DrawDragonTotem(SpriteBatch sb, float radius) {
            Texture2D lightTex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 lightOrigin = lightTex.Size() / 2f;

            // 中心龙威光球
            float coreScale = 0.8f + MathF.Sin(pulsePhase * 1.5f) * 0.2f;
            Color coreColor = Color.Gold;
            coreColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, coreColor, MathHelper.PiOver2, lightOrigin, coreScale * circleScale, SpriteEffects.None, 0f);

            // 外层光晕
            Color haloColor = Color.OrangeRed * 0.5f;
            haloColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, haloColor, MathHelper.PiOver2, lightOrigin, 1.5f * coreScale * circleScale, SpriteEffects.None, 0f);

            // 四方龙纹
            Texture2D waveTex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 waveOrigin = new Vector2(0, waveTex.Height / 2f);

            for (int i = 0; i < 4; i++) {
                float angle = runeRotation * 2f + MathHelper.PiOver2 * i;
                Vector2 dragonPos = Projectile.Center + angle.ToRotationVector2() * radius * 0.3f;

                Color dragonColor = Color.Lerp(Color.Gold, Color.OrangeRed, MathF.Sin(pulsePhase + i) * 0.5f + 0.5f) * 0.6f * circleScale;
                dragonColor.A = 0;

                sb.Draw(waveTex, dragonPos - Main.screenPosition, null, dragonColor, angle, waveOrigin,
                    new Vector2(0.5f * circleScale, 0.15f * circleScale), SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);

            // 消散特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 最终审判天雷
            if (Main.netMode != NetmodeID.MultiplayerClient && circleScale > 0.5f) {
                for (int i = 0; i < 5; i++) {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Vector2 targetPos = Projectile.Center + angle.ToRotationVector2() * 80f;
                    Vector2 lightningStart = targetPos + new Vector2(0, -700f);

                    Projectile.NewProjectile(Projectile.GetSource_Death(), lightningStart, new Vector2(0, 35f),
                        ModContent.ProjectileType<CelestialLightningBolt>(), Projectile.damage, 0f, Projectile.owner, targetPos.X, targetPos.Y);
                }
            }
        }
    }

    /// <summary>
    /// 天庭雷电 - 从天而降的金色神雷
    /// </summary>
    public class CelestialLightningBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private Vector2 targetPos;
        private bool hasStruck = false;
        private List<Vector2> lightningPoints = [];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void OnSpawn(IEntitySource source) {
            targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            GenerateLightningPath();
        }

        private void GenerateLightningPath() {
            lightningPoints.Clear();
            lightningPoints.Add(Projectile.Center);

            Vector2 direction = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
            float totalDistance = Vector2.Distance(Projectile.Center, targetPos);
            int segments = (int)(totalDistance / 40f);

            Vector2 currentPos = Projectile.Center;
            for (int i = 1; i < segments; i++) {
                float progress = (float)i / segments;
                Vector2 basePos = Vector2.Lerp(Projectile.Center, targetPos, progress);

                // 添加随机偏移形成闪电效果
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                float offset = Main.rand.NextFloat(-30f, 30f) * (1f - progress);
                currentPos = basePos + perpendicular * offset;

                lightningPoints.Add(currentPos);
            }

            lightningPoints.Add(targetPos);
        }

        public override void AI() {
            // 快速下降到目标位置
            if (!hasStruck) {
                Projectile.velocity *= 1.05f;

                if (Vector2.Distance(Projectile.Center, targetPos) < 50f || Projectile.timeLeft < 45) {
                    hasStruck = true;
                    Projectile.Center = targetPos;
                    Projectile.velocity = Vector2.Zero;

                    // 雷击爆发
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 0.8f }, targetPos);
                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), targetPos,
                        ACMWeaponBurst.GoldDragon, 1.3f, Projectile.owner);
                    WeaponVFX.AddScreenShake(targetPos, 3f);

                    for (int i = 0; i < 20; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                        int dust = Dust.NewDust(targetPos, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }

                    // 造成范围伤害
                    foreach (var npc in Main.npc) {
                        if (npc.active && !npc.friendly && !npc.dontTakeDamage &&
                            Vector2.Distance(npc.Center, targetPos) < 80f) {
                            npc.SimpleStrikeNPC(Projectile.damage, 0, false, 0);
                            npc.AddBuff(BuffID.Electrified, 120);
                        }
                    }
                }
            }
            else {
                // 雷击后快速消失
                if (Projectile.timeLeft > 20) {
                    Projectile.timeLeft = 20;
                }
            }

            // 闪电粒子
            if (!hasStruck && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * (hasStruck ? 0.5f : 1f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (lightningPoints.Count < 2) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D lightTex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 lightOrigin = lightTex.Size() / 2f;

            float alpha = hasStruck ? (Projectile.timeLeft / 20f) : 1f;

            // 绘制闪电路径
            for (int i = 0; i < lightningPoints.Count - 1; i++) {
                Vector2 start = lightningPoints[i];
                Vector2 end = lightningPoints[i + 1];
                Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
                float distance = Vector2.Distance(start, end);
                float rotation = direction.ToRotation();

                int steps = (int)(distance / 5f);
                for (int j = 0; j < steps; j++) {
                    float progress = (float)j / steps;
                    Vector2 pos = Vector2.Lerp(start, end, progress);

                    // 闪电核心
                    Color coreColor = Color.White * alpha;
                    coreColor.A = 0;
                    sb.Draw(lightTex, pos - Main.screenPosition, null, coreColor, rotation, lightOrigin, 0.3f, SpriteEffects.None, 0f);

                    // 外层光晕
                    Color glowColor = Color.Gold * 0.6f * alpha;
                    glowColor.A = 0;
                    sb.Draw(lightTex, pos - Main.screenPosition, null, glowColor, rotation, lightOrigin, 0.6f, SpriteEffects.None, 0f);
                }
            }

            // 雷击点光球
            if (hasStruck) {
                float pulseScale = 1f + MathF.Sin(Projectile.timeLeft * 0.5f) * 0.3f;
                Color strikeColor = Color.Gold * alpha;
                strikeColor.A = 0;
                sb.Draw(lightTex, targetPos - Main.screenPosition, null, strikeColor, 0f, lightOrigin, pulseScale * 1.5f, SpriteEffects.None, 0f);

                Color outerColor = Color.OrangeRed * 0.5f * alpha;
                outerColor.A = 0;
                sb.Draw(lightTex, targetPos - Main.screenPosition, null, outerColor, 0f, lightOrigin, pulseScale * 2.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? CanHitNPC(NPC target) => false; // 使用自定义伤害
    }
}

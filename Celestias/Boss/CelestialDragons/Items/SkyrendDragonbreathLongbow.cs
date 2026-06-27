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
    /// 裂穹 - 天庭巡卫金龙掉落的远程武器
    /// 由金龙吐息凝结而成的神弓，蕴含撕裂苍穹之力
    /// 特效：箭矢转化为龙息箭，命中后爆炸并产生龙息云，蓄力可释放裂天龙箭
    /// </summary>
    public class SkyrendDragonbreathLongbow : ModItem
    {
        private int chargeTime = 0;
        private const int MaxCharge = 45;
        private bool isFullyCharged = false;

        public override void SetDefaults() {
            Item.damage = 3860;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 30;
            Item.height = 68;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 35);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DragonbreathArrow>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 18;
            Item.channel = true;
        }

        public override void HoldItem(Player player) {
            if (player.channel && player.HasAmmo(Item)) {
                chargeTime++;

                // 蓄力效果
                if (chargeTime > 10 && Main.rand.NextBool(3)) {
                    float chargeProgress = Math.Min((chargeTime - 10) / (float)(MaxCharge - 10), 1f);
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(50 * (1 - chargeProgress), 50 * (1 - chargeProgress));
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f * chargeProgress);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }

                // 满蓄力提示
                if (chargeTime == MaxCharge) {
                    isFullyCharged = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.8f }, player.Center);

                    for (int i = 0; i < 12; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                        int dust = Dust.NewDust(player.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }

                if (chargeTime > MaxCharge) chargeTime = MaxCharge;
            }
            else if (chargeTime > 0 && !player.channel) {
                // 释放蓄力射击
                if (isFullyCharged) {
                    ShootFullCharge(player);
                }
                else if (chargeTime > 5) {
                    ShootNormal(player);
                }
                chargeTime = 0;
                isFullyCharged = false;
            }
        }

        private void ShootNormal(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            player.PickAmmo(Item, out int projType, out float speed, out int damage, out float knockback, out int usedAmmoItemId);

            // 发射3支龙息箭
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = direction.RotatedBy(MathHelper.ToRadians(5 * i)) * (Item.shootSpeed + speed);
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, vel,
                    ModContent.ProjectileType<DragonbreathArrow>(), damage + Item.damage, knockback + Item.knockBack, player.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.2f }, player.Center);
        }

        private void ShootFullCharge(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            player.PickAmmo(Item, out int projType, out float speed, out int damage, out float knockback, out int usedAmmoItemId);

            // 发射裂天龙箭
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, direction * (Item.shootSpeed * 1.5f + speed),
                ModContent.ProjectileType<SkyrendDragonArrow>(), (damage + Item.damage) * 2, (knockback + Item.knockBack) * 2f, player.whoAmI);

            // 两侧的龙息箭
            for (int i = -1; i <= 1; i += 2) {
                Vector2 vel = direction.RotatedBy(MathHelper.ToRadians(15 * i)) * (Item.shootSpeed + speed);
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, vel,
                    ModContent.ProjectileType<DragonbreathArrow>(), damage + Item.damage, knockback + Item.knockBack, player.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 0.7f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.4f, Volume = 1.2f }, player.Center);

            // 后坐力
            player.velocity -= direction * 5f;

            // 释放特效
            for (int i = 0; i < 20; i++) {
                Vector2 vel = direction.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(5f, 10f);
                int dust = Dust.NewDust(player.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 使用自定义射击逻辑
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "SkyrendLore", "「金龙一息，苍穹为裂」"));
            tooltips.Add(new TooltipLine(Mod, "SkyrendEffect", "箭矢转化为龙息箭，命中后爆炸并产生龙息云"));
            tooltips.Add(new TooltipLine(Mod, "SkyrendEffect2", "蓄力射击可释放裂天龙箭，造成双倍伤害"));
            tooltips.Add(new TooltipLine(Mod, "SkyrendEffect3", "龙息云会持续灼烧范围内敌人"));
        }
    }

    /// <summary>
    /// 龙息箭 - 普通射击释放的金色龙息箭矢
    /// </summary>
    public class DragonbreathArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙息火焰粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            // 金色尾迹粒子
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.2f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
            CreateDragonbreathExplosion(target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GoldDragon, 0.9f, Projectile.owner);
        }

        public override void OnKill(int timeLeft) {
            CreateDragonbreathExplosion(Projectile.Center);
        }

        private void CreateDragonbreathExplosion(Vector2 position) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.5f }, position);

            // 爆炸粒子
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(position, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 生成龙息云
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_Death(), position, Vector2.Zero,
                    ModContent.ProjectileType<DragonbreathCloud>(), Projectile.damage / 3, 0f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(200, 110, 25, 120), innerColor: new Color(255, 240, 170, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 龙息拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Gold, Color.OrangeRed, 1f - progress) * progress * 0.7f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.rotation, origin, 0.8f * progress * 0.5f, SpriteEffects.None, 0f);
            }

            // 箭矢主体
            Color arrowColor = Color.Gold;
            arrowColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, arrowColor, Projectile.rotation, origin, 0.8f * 0.5f, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 裂天龙箭 - 蓄力射击释放的强力龙形箭矢
    /// </summary>
    public class SkyrendDragonArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float dragonScale = 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 25;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.scale = 1.5f;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙形波动
            float wave = MathF.Sin(Projectile.ai[0] * 0.15f) * 0.15f;
            dragonScale = 1f + wave;
            Projectile.ai[0]++;

            // 龙身粒子效果
            for (int i = 0; i < 4; i++) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                float offset = MathF.Sin(Projectile.ai[0] * 0.3f + i * 0.5f) * 15f;
                Vector2 dustPos = Projectile.Center + perpendicular * offset - Projectile.velocity.SafeNormalize(Vector2.Zero) * i * 8f;

                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.05f;
            }

            // 轻微追踪
            NPC target = FindClosestNPC(400f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.02f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * dragonScale);
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
            target.AddBuff(BuffID.OnFire3, 300);

            // 裂天爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10, 10);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 3f);
                Main.dust[dust].noGravity = true;
            }

            // 生成多个龙息云
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 cloudPos = target.Center + Main.rand.NextVector2Circular(50, 50);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), cloudPos, Vector2.Zero,
                        ModContent.ProjectileType<DragonbreathCloud>(), Projectile.damage / 4, 0f, Projectile.owner);
                }
            }

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 0.6f }, target.Center);

            // 裂天龙箭·处决级金龙演出
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GoldDragon, 2f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 裂天龙箭招牌演出: 蛇形金龙身段 ribbon + 金芒径向辉光
            var ribbon = new List<Vector2>(Projectile.oldPos.Length);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                ribbon.Add(Projectile.oldPos[i] + Projectile.Size / 2f);
            }
            if (ribbon.Count >= 2)
                WeaponVFX.DrawRibbonTrail(ribbon.ToArray(), baseWidth: 34f * Projectile.scale,
                    outerColor: new Color(200, 110, 25, 150), innerColor: new Color(255, 240, 170, 190),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.11f * dragonScale, 0.5f, new Color(255, 210, 110), 6f);

            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);

            // 龙形拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float bodyScale = progress * dragonScale * (0.7f + MathF.Sin(Projectile.ai[0] * 0.2f + i * 0.3f) * 0.3f);

                Color trailColor = Color.Lerp(Color.Gold, Color.OrangeRed, 1f - progress);
                trailColor *= progress * 0.8f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1.5f * bodyScale, 0.4f * bodyScale) * Projectile.scale * 0.5f, SpriteEffects.None, 0f);
            }

            // 龙头主体
            Color headColor = Color.Gold;
            headColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, headColor, Projectile.rotation,
                origin, new Vector2(2f * dragonScale, 0.6f * dragonScale) * Projectile.scale * 0.5f, SpriteEffects.None, 0f);

            // 龙眼光点
            Texture2D eyeTexture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 eyePos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
            Color eyeColor = Color.White;
            eyeColor.A = 0;
            Main.spriteBatch.Draw(eyeTexture, eyePos - Main.screenPosition, null, eyeColor, 0f, eyeTexture.Size() / 2f, 0.4f * dragonScale * 0.5f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);

            // 裂天爆炸
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12, 12);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // 生成龙息云群
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 5; i++) {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Vector2 cloudPos = Projectile.Center + angle.ToRotationVector2() * 60f;
                    Projectile.NewProjectile(Projectile.GetSource_Death(), cloudPos, Vector2.Zero,
                        ModContent.ProjectileType<DragonbreathCloud>(), Projectile.damage / 5, 0f, Projectile.owner);
                }
            }
        }
    }

    /// <summary>
    /// 龙息云 - 持续灼烧区域的金色火焰云
    /// </summary>
    public class DragonbreathCloud : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float cloudScale = 0f;
        private float cloudPhase = 0f;
        private int damageTimer = 0;

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            cloudPhase += 0.1f;
            damageTimer++;

            // 云雾展开
            if (Projectile.timeLeft > 100) {
                cloudScale = MathHelper.Lerp(cloudScale, 1.2f, 0.1f);
            }
            // 云雾消散
            else if (Projectile.timeLeft < 30) {
                cloudScale = MathHelper.Lerp(cloudScale, 0f, 0.08f);
            }

            // 调整碰撞范围
            Projectile.width = Projectile.height = (int)(80 * cloudScale);

            // 云雾粒子
            if (Main.rand.NextBool(3) && cloudScale > 0.3f) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(30f * cloudScale);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * dist;

                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Smoke;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1f, 100, default, 1.5f * cloudScale);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            // 周期性对范围内敌人造成伤害
            if (damageTimer >= 15 && cloudScale > 0.5f) {
                damageTimer = 0;
                DealCloudDamage();
            }

            // 缓慢上升
            Projectile.velocity = new Vector2(0, -0.3f);

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.7f, 0.2f) * cloudScale * 0.5f);
        }

        private void DealCloudDamage() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float damageRadius = 50f * cloudScale;

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage &&
                    Vector2.Distance(npc.Center, Projectile.Center) < damageRadius) {
                    npc.SimpleStrikeNPC(Projectile.damage, 0, false, 0);
                    npc.AddBuff(BuffID.OnFire, 60);

                    // 灼烧效果
                    for (int i = 0; i < 3; i++) {
                        int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.GoldFlame, 0, -1f, 100, default, 1f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
        }

        public override bool? CanHitNPC(NPC target) => false; // 使用自定义伤害

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.Smoke ?? TextureAssets.Projectile[Type].Value;

            // 烟雾帧动画
            int frameSize = texture.Width / 4;
            int frameX = (int)(cloudPhase * 2f) % 4;
            int frameY = ((int)(cloudPhase * 2f) / 4) % 4;
            Rectangle sourceRect = new Rectangle(frameX * frameSize, frameY * frameSize, frameSize, frameSize);
            Vector2 origin = new Vector2(frameSize / 2f, frameSize / 2f);

            // 多层云雾
            for (int layer = 0; layer < 3; layer++) {
                float layerOffset = layer * 0.3f;
                float layerScale = (1f - layer * 0.2f) * cloudScale;
                float rotation = cloudPhase * (layer % 2 == 0 ? 0.3f : -0.3f);

                Color cloudColor = Color.Lerp(Color.Gold, Color.OrangeRed, layer * 0.3f) * (0.5f - layer * 0.1f);
                cloudColor.A = 0;

                Vector2 drawPos = Projectile.Center + new Vector2(MathF.Cos(cloudPhase + layerOffset), MathF.Sin(cloudPhase * 1.3f + layerOffset)) * 5f * cloudScale;
                Main.spriteBatch.Draw(texture, drawPos - Main.screenPosition, sourceRect, cloudColor, rotation, origin, layerScale * 1.2f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y - 1f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

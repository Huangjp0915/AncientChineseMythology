using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons.Items
{
    /// <summary>
    /// 逆鳞 - 天庭巡卫金龙掉落的近战武器
    /// 由金龙逆鳞锻造而成的神刀，蕴含龙威之力
    /// 特效：挥砍释放金色龙气斩，每第三刀释放巨龙咆哮，命中积累龙威可触发逆鳞之怒
    /// </summary>
    public class ScalebreakerCleaver : ModItem
    {
        private int slashCount = 0;
        private float dragonFury = 0f;
        private const float MaxFury = 100f;

        public override void SetDefaults() {
            Item.damage = 4680;
            Item.DamageType = DamageClass.Melee;
            Item.width = 114;
            Item.height = 336;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(gold: 35);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<ScalebreakerCleaverProjectile>();
            Item.shootSpeed = 12f;
            Item.crit = 15;
        }

        public override void HoldItem(Player player) {
            // 龙威光环效果
            if (dragonFury > 50f && Main.rand.NextBool(8)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(40, 40);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, -1f, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            // 满怒气时的强烈效果
            if (dragonFury >= MaxFury && Main.rand.NextBool(4)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(60, 60);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, -2f, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            // 怒气自然衰减
            if (dragonFury > 0f) {
                dragonFury -= 0.05f;
                if (dragonFury < 0f) dragonFury = 0f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            slashCount++;
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 普通龙气斩
            Projectile.NewProjectile(source, player.Center + direction * 30f, direction * 14f,
                ModContent.ProjectileType<DragonAuraSlash>(), damage, knockback, player.whoAmI);

            // 每第三刀释放巨龙咆哮
            if (slashCount >= 3) {
                slashCount = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 0.8f }, player.Center);

                // 巨龙咆哮波
                Projectile.NewProjectile(source, player.Center, direction * 18f,
                    ModContent.ProjectileType<DragonRoarWave>(), (int)(damage * 1.5f), knockback * 2f, player.whoAmI);

                // 屏幕震动
                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 10);
                }
            }

            // 满怒气时触发逆鳞之怒
            if (dragonFury >= MaxFury) {
                dragonFury = 0f;
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f, Volume = 1.2f }, player.Center);

                // 逆鳞之怒 - 全方位龙气爆发
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 dir = angle.ToRotationVector2();
                    Projectile.NewProjectile(source, player.Center, dir * 16f,
                        ModContent.ProjectileType<ReverseScaleWrath>(), damage * 2, knockback, player.whoAmI);
                }

                // 视觉爆发
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10, 10);
                    int dust = Dust.NewDust(player.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 累积龙威
            dragonFury += 8f;
            if (hit.Crit) dragonFury += 12f;
            if (dragonFury > MaxFury) dragonFury = MaxFury;

            // 金色龙鳞碎片效果
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "「触碰龙之逆鳞者，必遭龙威之怒」"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "挥砍释放金色龙气斩"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "每第三刀释放巨龙咆哮波"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect3", "命中敌人积累龙威，满怒时触发逆鳞之怒"));
        }
    }

    /// <summary>
    /// 逆鳞刀弹幕 - 手持挥砍
    /// </summary>
    public class ScalebreakerCleaverProjectile : ModProjectile
    {
        private float swingProgress = 0f;
        private float swingDirection = 1f;
        private const float SwingAngle = MathHelper.Pi * 0.8f;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void OnSpawn(IEntitySource source) {
            swingDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            Projectile.spriteDirection = (int)swingDirection;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            swingProgress += 0.08f;
            if (swingProgress >= 1f) {
                Projectile.Kill();
                return;
            }

            // 挥砍弧度
            float baseAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            float currentAngle = baseAngle + (swingProgress - 0.5f) * SwingAngle * swingDirection;
            Projectile.rotation = currentAngle;

            Vector2 direction = currentAngle.ToRotationVector2();
            Projectile.Center = Owner.MountedCenter + direction * 60f;

            Owner.direction = swingDirection > 0 ? 1 : -1;
            float armRotation = currentAngle - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            // 挥砍粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + direction * 40f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = direction.RotatedBy(MathHelper.PiOver2 * swingDirection) * 3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(texture.Width / 2, texture.Height);
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 towner = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float rotfix = Projectile.spriteDirection > 0 ? 0 : MathHelper.PiOver2 + MathHelper.PiOver2;

            //// 拖尾
            for (int i = 0; i < Projectile.oldPos.Length - 8; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Gold * progress * 0.5f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i] + rotfix, origin, Projectile.scale * progress * 0.5f, effects, 0);
            }

            // 发光层
            Color glowColor = Color.Gold * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation + rotfix, origin, Projectile.scale * 0.6f, effects, 0);

            // 主体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotfix, origin, Projectile.scale * 0.5f, effects, 0);

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * 100f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f, ref collisionPoint);
        }
    }

    /// <summary>
    /// 龙气斩 - 普通攻击释放的金色气刃
    /// </summary>
    public class DragonAuraSlash : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.98f;

            // 龙气粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 5);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.2f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 120);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Gold, Color.Orange, 1f - progress) * progress * 0.6f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(0.8f * progress, 0.15f * progress) * 0.5f, SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = Color.Gold;
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation,
                origin, new Vector2(0.8f, 0.2f) * 0.5f, SpriteEffects.None, 0f);

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
    /// 巨龙咆哮波 - 每第三刀释放的强力冲击波
    /// </summary>
    public class DragonRoarWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float waveScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            waveScale = MathHelper.Lerp(waveScale, 1.5f, 0.08f);

            // 调整碰撞箱
            Projectile.width = (int)(80 * waveScale);
            Projectile.height = (int)(60 * waveScale);

            // 龙形波动粒子
            for (int i = 0; i < 3; i++) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                float offset = MathF.Sin(Projectile.ai[0] * 0.3f + i) * 20f * waveScale;
                Vector2 dustPos = Projectile.Center + perpendicular * offset;

                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2f * waveScale);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.3f) * waveScale);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);

            // 巨龙咆哮冲击效果
            Vector2 knockbackDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            target.velocity += knockbackDir * 8f;

            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 0.5f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);

            // 龙形拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float bodyScale = progress * waveScale * (0.8f + MathF.Sin(Projectile.ai[0] * 0.2f + i * 0.4f) * 0.2f);

                Color trailColor = Color.Lerp(Color.Gold, Color.OrangeRed, 1f - progress);
                trailColor *= progress * 0.7f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1.2f * bodyScale, 0.35f * bodyScale) * 0.5f, SpriteEffects.None, 0f);
            }

            // 龙头主体
            Color headColor = Color.Gold;
            headColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, headColor, Projectile.rotation,
                origin, new Vector2(1.5f * waveScale, 0.5f * waveScale) * 0.5f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);

            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 逆鳞之怒 - 满怒气触发的强力龙气弹
    /// </summary>
    public class ReverseScaleWrath : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.scale = 1.2f;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙鳞能量粒子
            for (int i = 0; i < 2; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 15);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            // 追踪最近敌人
            if (Projectile.timeLeft < 60) {
                NPC target = FindClosestNPC(600f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.05f);
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.2f) * 0.8f);
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
            target.AddBuff(BuffID.Ichor, 180);

            // 逆鳞爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10, 10);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.5f, Volume = 0.7f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 龙鳞能量拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Gold, Color.OrangeRed, 1f - progress) * progress * 0.8f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailScale = 1.2f * progress * Projectile.scale;
                Main.spriteBatch.Draw(texture, drawPos, null, trailColor, Projectile.rotation, origin, trailScale, SpriteEffects.None, 0f);
            }

            // 核心光球
            Color coreColor = Color.Gold;
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, coreColor, Projectile.rotation, origin, 1.5f * Projectile.scale, SpriteEffects.None, 0f);

            // 外层光晕
            Color outerColor = Color.OrangeRed * 0.5f;
            outerColor.A = 0;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, outerColor, Projectile.rotation, origin, 2f * Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);

            // 逆鳞爆炸
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10, 10);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

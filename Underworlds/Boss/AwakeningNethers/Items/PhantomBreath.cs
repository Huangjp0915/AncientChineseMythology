using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers.Items
{
    /// <summary>
    /// 幻影龙息 - 觉醒幽冥龙掉落的射手武器
    /// 由幽冥龙的吐息凝结而成的幻影之弓
    /// 特效：箭矢转化为幻影龙息，命中后分裂追踪，蓄力可发射毁灭龙息
    /// </summary>
    public class PhantomBreath : ModItem
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/PhantomBreath";

        private int chargeTime = 0;
        private const int MaxCharge = 60;

        public override void SetDefaults() {
            Item.damage = 6420;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 32;
            Item.height = 64;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<PhantomBreathArrow>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 12;
            Item.channel = true; // 启用蓄力
        }

        public override void HoldItem(Player player) {
            // 蓄力逻辑
            if (player.channel && player.HasAmmo(Item)) {
                chargeTime++;
                // 满蓄力提示
                if (chargeTime == MaxCharge - 1) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.8f }, player.Center);
                    AwakeningNetherHelper.CreateSoulBurst(player.Center, 40f, 1, 8);
                }
                if (chargeTime > MaxCharge) chargeTime = MaxCharge;

                // 蓄力粒子效果
                if (chargeTime > 20 && Main.rand.NextBool(3)) {
                    float chargeProgress = (chargeTime - 20) / (float)(MaxCharge - 20);
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(50 * (1 - chargeProgress), 50 * (1 - chargeProgress));
                    var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1f + chargeProgress;
                    d.velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 3f);
                }
            }
            else if (chargeTime > 0) {
                // 释放蓄力
                if (chargeTime >= MaxCharge) {
                    // 满蓄力 - 发射毁灭龙息
                    ShootChargedBreath(player);
                }
                chargeTime = 0;
            }
        }

        private void ShootChargedBreath(Player player) {
            if (Main.myPlayer != player.whoAmI) return;

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            int damage = (int)(Item.damage * 2.5f);

            // 发射毁灭龙息
            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                player.Center,
                direction * Item.shootSpeed * 1.5f,
                ModContent.ProjectileType<PhantomDevastation>(),
                damage,
                Item.knockBack * 2f,
                player.whoAmI
            );

            // 消耗额外弹药
            player.PickAmmo(Item, out _, out _, out _, out _, out _, true);

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1.2f }, player.Center);
            AwakeningNetherHelper.CreateVoidVortex(player.Center, 80f, 0.8f, 25);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // 转换为幻影龙息箭
            type = ModContent.ProjectileType<PhantomBreathArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 发射主箭矢
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai0: 0);

            // 30%几率发射额外的幽魂箭
            if (Main.rand.NextBool(3)) {
                Vector2 offsetVel = velocity.RotatedByRandom(MathHelper.ToRadians(10)) * 0.95f;
                Projectile.NewProjectile(source, position, offsetVel, type, (int)(damage * 0.6f), knockback * 0.5f, player.whoAmI, ai0: 1);
            }

            return false;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-4, 0);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "PhantomLore", "「凝聚幽冥龙息的幻影之弓，箭矢化为吞噬一切的龙息」"));
            tooltips.Add(new TooltipLine(Mod, "PhantomEffect", "箭矢命中后分裂为追踪幽魂"));
            tooltips.Add(new TooltipLine(Mod, "PhantomEffect2", "蓄力满后释放毁灭龙息（按住左键蓄力）"));
        }
    }

    /// <summary>
    /// 幻影龙息箭 - 主要箭矢弹幕
    /// </summary>
    public class PhantomBreathArrow : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/PhantomBreathArrow";

        private float pulsePhase = 0f;
        private bool IsGhost => Projectile.ai[0] > 0; // 是否为幽魂版本

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 幽魂版本的追踪行为
            if (IsGhost && Projectile.timeLeft < 150) {

            }

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                int dustType = IsGhost ? DustID.SpectreStaff : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5, 5), dustType);
                d.noGravity = true;
                d.scale = IsGhost ? 0.8f : 1.1f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = IsGhost
                    ? AwakeningNetherHelper.NetherCyan * progress * 0.5f
                    : AwakeningNetherHelper.AwakeningPurple * progress * 0.6f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.5f + progress * 0.5f), SpriteEffects.None, 0);
            }

            // 主体
            Color mainColor = IsGhost ? AwakeningNetherHelper.NetherCyan : AwakeningNetherHelper.AwakeningPurple;

            // 光晕
            Color glowColor = mainColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.5f,
                Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor with { A = 0 },
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时分裂为追踪幽魂
            if (!IsGhost && Main.myPlayer == Projectile.owner) {
                int splitCount = 3;
                for (int i = 0; i < splitCount; i++) {
                    float angle = MathHelper.TwoPi * i / splitCount + Main.rand.NextFloat(-0.3f, 0.3f);
                    Vector2 splitVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        target.Center,
                        splitVel,
                        ModContent.ProjectileType<PhantomSplitGhost>(),
                        (int)(Projectile.damage * 0.4f),
                        Projectile.knockBack * 0.3f,
                        Projectile.owner
                    );
                }
            }

            target.AddBuff(BuffID.ShadowFlame, 180);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);

            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 幻影分裂幽魂 - 命中后分裂的追踪弹幕
    /// </summary>
    public class PhantomSplitGhost : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float pulsePhase = 0f;
        private float homingDelay = 15f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            pulsePhase += 0.15f;
            homingDelay--;

            // 延迟后开始追踪
            if (homingDelay <= 0) {
                NPC target = FindTarget(500f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float speed = MathHelper.Lerp(Projectile.velocity.Length(), 14f, 0.05f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * speed, 0.1f);
                }
            }

            Projectile.rotation += 0.1f;

            // 幽魂粒子
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.NetherCyan.ToVector3() * 0.3f);
        }

        private NPC FindTarget(float range) {
            NPC closest = null;
            float closestDist = range;
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
            SpriteBatch sb = Main.spriteBatch;

            // 使用高级核心绘制
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center,
                AwakeningNetherHelper.NetherCyan,
                AwakeningNetherHelper.SoulPink,
                0.8f * pulse, pulsePhase);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }

    /// <summary>
    /// 毁灭龙息 - 满蓄力释放的超强弹幕
    /// </summary>
    public class PhantomDevastation : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float pulsePhase = 0f;
        private float growthScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            pulsePhase += 0.15f;
            growthScale = MathHelper.Lerp(growthScale, 2f, 0.03f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 大量粒子效果
            AwakeningNetherHelper.CreateVoidTrail(Projectile.Center, Projectile.velocity, growthScale);

            // 周围产生虚空漩涡
            if (Main.rand.NextBool(3)) {
                AwakeningNetherHelper.CreateVoidVortex(Projectile.Center, 50f * growthScale, 0.3f, 5);
            }

            // 发光
            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * growthScale);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制巨型拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                float trailScale = growthScale * progress;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2;

                AwakeningNetherHelper.DrawVoidCore(sb, drawPos,
                    AwakeningNetherHelper.VoidDarkPurple * progress * 0.5f,
                    AwakeningNetherHelper.AwakeningPurple * progress * 0.3f,
                    trailScale, pulsePhase + i * 0.2f);
            }

            // 主体
            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center,
                AwakeningNetherHelper.AwakeningPurple,
                AwakeningNetherHelper.DestructionRed,
                growthScale * 1.5f, pulsePhase, true);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时造成次元撕裂
            AwakeningNetherHelper.CreateDimensionTear(Projectile.Center, target.Center, 0.8f);
            AwakeningNetherHelper.CreateSoulBurst(target.Center, 60f, 2, 12);

            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);

            // 爆炸特效
            AwakeningNetherHelper.CreateVoidVortex(Projectile.Center, 150f, 1.5f, 50);
            AwakeningNetherHelper.CreateSoulBurst(Projectile.Center, 120f, 4, 20);
            AwakeningNetherHelper.CreateScreenFlash(Projectile.Center, AwakeningNetherHelper.AwakeningPurple, 0.6f);
        }
    }
}

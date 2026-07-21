using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 璇玑玉轮 - 天柱敌怪掉落的回旋镖类武器 (系列旗舰之一)。
    /// 机制身份: 玉衡仪 — 左键掷出浑仪玉轮 (命中三次散星珠);
    /// 右键展开"张衡浑天阵"(专属 PillarArmillaryRing 着色器领域): 六枚星官玉珠沿三环轨道运行,
    /// 每秒对阵内最近敌人轰落天罚落雷。决策点: 贴身领域窗口与 10s 冷却管理。
    /// </summary>
    public class JadeArmillary : ModItem
    {
        private uint fieldReadyTime; // 浑天阵冷却 (CanUseItem/Shoot 仅 owner 端参与, 实例字段安全)

        public override void SetDefaults() {
            Item.damage = 180;
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

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 右键: 冷却就绪且场上无阵才可展开
                return Main.GameUpdateCount >= fieldReadyTime
                    && player.ownedProjectileCounts[ModContent.ProjectileType<ArmillarySphereField>()] == 0;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<JadeArmillaryProjectile>()] < 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                // —— 右键·张衡浑天阵 (6s 持续, 10s 冷却) ——
                fieldReadyTime = Main.GameUpdateCount + 600;
                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<ArmillarySphereField>(), (int)(damage * 0.5f), knockback * 0.5f, player.whoAmI);

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.9f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item123 with { Pitch = 0.15f, Volume = 0.7f }, player.Center);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "仿天柱浑天仪铸造的神器"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "掷出旋转玉轮，每命中三次释放追踪星珠"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "右键展开张衡浑天阵：六枚星官玉珠沿环轨道护体，每秒对阵内敌人轰落天罚落雷（冷却10秒）"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 张衡浑天阵 - 以玩家为中心的浑天仪领域 (专属 PillarArmillaryRing 着色器)。
    /// 六枚星官玉珠沿三道倾角环轨道运行 (珠体即伤害判定, 视觉与判定严格对齐);
    /// 每 60 帧对阵内最近敌人轰落天罚落雷 (0.7×武器)。
    /// </summary>
    public class ArmillarySphereField : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int Life = 360;
        private const float MaxRadius = 170f;
        private const float JudgeRadius = 260f;

        private int Age => Life - Projectile.timeLeft;
        private float Radius => MaxRadius * ACMUtils.QuadOut(MathHelper.Clamp(Age / 20f, 0f, 1f));
        private float FadeOut => MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>
        /// 第 i 枚星官玉珠的世界坐标 (与着色器 RingBand 同一套环参数的逆变换,
        /// 珠体视觉严格落在着色器环上)。i/2 = 环序 (0 外 1 中 2 内), i%2 = 对位双珠。
        /// </summary>
        private Vector2 BeadPos(int i, float time) {
            int ring = i / 2;
            (float rScale, float spin, float tiltPhase, float orbit) = ring switch {
                0 => (1.00f, time * 0.45f, time * 0.60f, time * 1.6f),
                1 => (0.80f, -time * 0.60f, time * 0.45f + 2.1f, -time * 1.3f),
                _ => (0.62f, time * 0.80f, time * 0.75f + 4.2f, time * 2.0f),
            };
            float squash = MathHelper.Lerp(0.24f, 0.95f, 0.5f + 0.5f * MathF.Sin(tiltPhase));
            float theta = orbit + (i % 2) * MathF.PI + ring * 1.05f;

            // 圆参数点 → 纵向压扁 (倾角) → 逆旋回世界系 (着色器把世界差矢正旋 spin 进环系)
            Vector2 local = new(MathF.Cos(theta) * Radius * rScale, MathF.Sin(theta) * Radius * rScale * squash);
            return Projectile.Center + local.RotatedBy(-spin);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            // 领域随玩家
            Projectile.Center = Owner.Center;

            // 展开瞬间: 震屏 (泛光在 PreDraw 走名额)
            if (Age == 1)
                WeaponVFX.AddScreenShake(Owner.Center, 4f);

            // 每 60 帧: 对阵内最近敌人落天罚雷 (owner 端闭环)
            if (Age > 20 && Age % 60 == 0 && Projectile.owner == Main.myPlayer) {
                NPC best = null;
                float bestDist = JudgeRadius;
                foreach (NPC npc in Main.npc) {
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) continue;
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best = npc;
                    }
                }
                if (best != null) {
                    HeavenJudgmentBolt.Strike(Projectile.GetSource_FromThis(), best.Center,
                        (int)(Projectile.damage * 1.4f), 3f, Projectile.owner, 0.9f);
                }
            }

            // 玉珠轨道流尘
            float t = (float)Main.GlobalTimeWrappedHourly;
            if (Main.rand.NextBool(2)) {
                int bead = Main.rand.Next(6);
                Vector2 pos = BeadPos(bead, t);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                Dust d = Dust.NewDustPerfect(pos, dustType, Main.rand.NextVector2Circular(1f, 1f), 130, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, PillarPalette.Gold.ToVector3() * 0.6f * FadeOut);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 判定 = 六枚玉珠珠体 (与视觉严格对齐), 领域本身不判伤
            if (Age < 12)
                return false;
            float t = (float)Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < 6; i++) {
                Vector2 bp = BeadPos(i, t);
                var beadBox = new Rectangle((int)bp.X - 20, (int)bp.Y - 20, 40, 40);
                if (targetHitbox.Intersects(beadBox))
                    return true;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.8f, Projectile.owner);
            SoundEngine.PlaySound(SoundID.Item101 with { Pitch = 0.4f, Volume = 0.4f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            float intensity = 0.5f * FadeOut * MathHelper.Clamp(Age / 12f, 0f, 1f); // 加性 decal 强度克制 (≤0.5)
            if (intensity <= 0.01f)
                return false;

            // 展开泛光 (前 10 帧, 占全屏名额, 满则自动退化柔光)
            if (Age < 10)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.16f, 0.55f * (1f - Age / 10f), PillarPalette.Gold, 10f);

            // 浑天仪三环领域 (屏幕空间 decal, 不占全屏名额)
            Effect fx = WeaponVFX.GetEffect("PillarArmillaryRing");
            if (fx != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, Radius, out Vector2 uvCenter, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uvCenter);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(intensity);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(PillarPalette.Gold.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(PillarPalette.SkyCyan.ToVector4());
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            }

            // 六枚星官玉珠 (BlankStar + SoftGlow, 与判定同坐标)
            Texture2D star = ACMAsset.BlankStar;
            Texture2D glow = ACMAsset.SoftGlow;
            if (star != null) {
                float t = (float)Main.GlobalTimeWrappedHourly;
                for (int i = 0; i < 6; i++) {
                    Vector2 bp = BeadPos(i, t) - Main.screenPosition;
                    Color c = (i % 2 == 0 ? PillarPalette.Gold : PillarPalette.HolyWhite) * FadeOut;
                    c.A = 0;
                    if (glow != null)
                        Main.spriteBatch.Draw(glow, bp, null, c * 0.7f, 0f, glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(star, bp, null, c, t * 3f + i, star.Size() * 0.5f, 0.24f, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.35f, Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 玉衡浑仪弹幕 - 掷出的浑仪玉轮 (本体贴图 + 程序化双环珠点, 自转速度随飞行速度门控)
    /// </summary>
    public class JadeArmillaryProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/JadeArmillary";

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

            // 自转速度随飞行速度门控 (快时急旋, 回收时舒缓 — 速度感靠对比)
            Projectile.rotation += 0.12f + Projectile.velocity.Length() * 0.02f;

            if (!returning) {
                Projectile.velocity *= 0.97f;
                if (Projectile.velocity.Length() < 4f || Projectile.timeLeft < 240) {
                    returning = true;
                }
            }
            else {
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

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.9f, Projectile.owner);

            // 每命中3次释放星辰碎片
            if (hitCounter >= 3) {
                hitCounter = 0;
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                    ACMWeaponBurst.HeavenlyPillar, 1.3f, Projectile.owner);

                if (Projectile.owner == Main.myPlayer) {
                    for (int i = 0; i < 4; i++) {
                        float angle = MathHelper.TwoPi * i / 4 + Projectile.rotation;
                        Vector2 vel = angle.ToRotationVector2() * 8f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                            ModContent.ProjectileType<CelestialFragment>(), Projectile.damage / 2, 2f, Projectile.owner);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 浑仪金青双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 12f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.3f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 旋转拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 程序化双环珠点 (微型浑天仪: 两道倾角摆动椭圆环, 各 6 珠)
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                float t = (float)Main.GlobalTimeWrappedHourly;
                Vector2 screenPos = Projectile.Center - Main.screenPosition;
                for (int ring = 0; ring < 2; ring++) {
                    float spin = Projectile.rotation * (ring == 0 ? 1f : -0.7f);
                    float squash = MathHelper.Lerp(0.3f, 0.9f, 0.5f + 0.5f * MathF.Sin(t * (1.2f - ring * 0.4f) + ring * 2f));
                    float r = 20f + ring * 7f;
                    for (int i = 0; i < 6; i++) {
                        float theta = spin * 1.4f + MathHelper.TwoPi * i / 6f;
                        Vector2 local = new(MathF.Cos(theta) * r, MathF.Sin(theta) * r * squash);
                        Vector2 pos = screenPos + local.RotatedBy(-spin);
                        Color c = (ring == 0 ? PillarPalette.Gold : PillarPalette.SkyCyan) * 0.8f;
                        c.A = 0;
                        Main.spriteBatch.Draw(star, pos, null, c, theta, star.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
                    }
                }
            }

            // 外层光环
            Color outerGlow = Color.Gold * 0.4f;
            outerGlow.A = 0;
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

            // 追踪 (8 帧重锁, 目标缓存 localAI[0])
            if (++Projectile.localAI[1] % 8 == 0 || Projectile.localAI[0] == 0f)
                Projectile.localAI[0] = 1f + (FindClosestNPC(400f)?.whoAmI ?? -2);

            int targetId = (int)Projectile.localAI[0] - 1;
            if (targetId >= 0 && targetId < Main.npc.Length && Main.npc[targetId].active && Main.npc[targetId].CanBeChasedBy()) {
                Vector2 toTarget = (Main.npc[targetId].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 12f, 0.08f);
            }

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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.6f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

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

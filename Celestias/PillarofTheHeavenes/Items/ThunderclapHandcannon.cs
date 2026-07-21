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
    /// 天柱武器系列共享配色 (金白祥瑞 + 天青云气 + 雷霆青白)。
    /// 与 <see cref="ACMWeaponBurst.HeavenlyPillar"/> 主题同源, 系列七件统一消费。
    /// </summary>
    public static class PillarPalette
    {
        /// <summary>暖祥金。</summary>
        public static readonly Color Gold = new(255, 215, 120);
        /// <summary>瑞白 (高光芯)。</summary>
        public static readonly Color HolyWhite = new(255, 250, 220);
        /// <summary>天青云气。</summary>
        public static readonly Color SkyCyan = new(140, 215, 235);
        /// <summary>雷霆青白 (对齐 TelegraphColors.Lightning)。</summary>
        public static readonly Color Lightning = new(190, 235, 255);
        /// <summary>深青 (外缘/暗部)。</summary>
        public static readonly Color DeepAzure = new(60, 120, 190);
    }

    /// <summary>
    /// 天罚落雷 — 天柱系列贯穿语言: 关键命中时自天顶轰落的判罚雷光。
    /// 12 帧金环收缩预告(因果可读) → 贯天光柱 + 电弧 + 落点冲击环 + 震屏。
    /// 七件武器以各自机制(数发/连段/叠印/转轮/锁敌/轨道)触发同一原语。
    /// 伤害为竖直柱判定, 每道对同一敌人至多命中一次。
    /// </summary>
    public class HeavenJudgmentBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int TelegraphTime = 12;   // 预告帧数 (金环收缩)
        private const int StrikeTime = 24;      // 雷柱存在+衰减帧数
        private const float ColumnHeight = 860f;

        private float Scale => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];
        private int Age => TelegraphTime + StrikeTime - Projectile.timeLeft;

        /// <summary>
        /// 在世界点落一道天罚雷 (仅 owner 客户端生成并同步)。damage 传最终伤害值。
        /// </summary>
        public static void Strike(IEntitySource source, Vector2 worldPos, int damage, float knockback, int owner, float scale = 1f) {
            if (Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<HeavenJudgmentBolt>(), damage, knockback, owner, scale);
        }

        public override void SetStaticDefaults() {
            // 雷柱贯穿全屏高度, 落点在屏外上/下时也要绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphTime + StrikeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每道雷对同一敌人只判一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int age = Age;

            if (age < TelegraphTime) {
                // 预告: 金尘向落点收敛 (因果先行)
                if (Main.rand.NextBool(2)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f) * Scale;
                    Dust d = Dust.NewDustPerfect(from, DustID.GoldCoin, (Projectile.Center - from) * 0.12f, 120, default, 1.2f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, PillarPalette.Gold.ToVector3() * 0.4f);
                return;
            }

            if (age == TelegraphTime) {
                // 落雷帧: 冲击链 (声音分层 + 震屏 + 火花上溅)
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.7f, Pitch = 0.15f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.8f, Pitch = -0.1f + Main.rand.NextFloat(0.2f) }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 3.5f * MathHelper.Clamp(Scale, 0.7f, 1.4f));

                for (int i = 0; i < 16; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-9f, -2f));
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.GoldFlame;
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 6f), dustType, vel, 80, default, 1.8f);
                    d.noGravity = true;
                }
            }

            // 雷柱期: 柱身零星电花
            if (Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-16f, 16f) * Scale, Main.rand.NextFloat(ColumnHeight * 0.9f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 120, default, 1.1f);
                d.noGravity = true;
                d.velocity = new Vector2(0f, Main.rand.NextFloat(2f, 5f));
            }
            Lighting.AddLight(Projectile.Center, PillarPalette.Lightning.ToVector3() * 1.1f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int age = Age;
            // 只有落雷后的短窗判定 (视觉与伤害严格对齐)
            if (age < TelegraphTime || age > TelegraphTime + 10)
                return false;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - new Vector2(0f, ColumnHeight), Projectile.Center + new Vector2(0f, 26f),
                30f * Scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 天条锁 (天律法典) 使天罚雷伤害 +25%
            if (target.TryGetGlobalNPC(out DivineLawGlobalNPC law) && law.Locked)
                modifiers.FinalDamage *= 1.25f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.1f * Scale, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            Vector2 basePos = Projectile.Center;
            Vector2 topPos = basePos - new Vector2(0f, ColumnHeight);

            if (age < TelegraphTime) {
                // —— 预告: 金环收缩 + 极淡天光引线 ——
                float tt = age / (float)TelegraphTime;
                WeaponVFX.DrawShockwaveRing(basePos, MathHelper.Lerp(86f, 16f, ACMUtils.QuadIn(tt)) * Scale, 7f,
                    0.35f + 0.55f * tt, PillarPalette.HolyWhite, PillarPalette.Gold);
                ACMShaders.DrawBeam(topPos, basePos, 3f * Scale, PillarPalette.HolyWhite, PillarPalette.SkyCyan,
                    0.28f * tt, flowSpeed: 2.6f, coreSharp: 3f);
                return false;
            }

            // —— 落雷: 贯天光柱 + 双层电弧 + 扩张冲击环 ——
            float st = (age - TelegraphTime) / (float)StrikeTime;
            float fade = 1f - st;

            ACMShaders.DrawBeam(topPos, basePos, 26f * Scale * (0.55f + 0.45f * fade),
                PillarPalette.HolyWhite with { A = 235 }, PillarPalette.SkyCyan with { A = 120 },
                fade, flowSpeed: 3.4f, flowScale: 1.5f, coreSharp: 3.2f, coreGlow: 0.9f);

            Texture2D branch = ACMAsset.LightningBranch;
            if (branch != null && fade > 0.05f) {
                // 抖动电弧: 每 3 帧换相位, 双层错开
                float seedBase = Projectile.whoAmI * 2.39f + (age / 3) * 1.71f;
                for (int layer = 0; layer < 2; layer++) {
                    float xOff = MathF.Sin(seedBase + layer * 2.6f) * 12f * Scale;
                    float sx = (0.55f + layer * 0.35f) * Scale;
                    Color c = (layer == 0 ? PillarPalette.Lightning : PillarPalette.Gold) * (fade * (0.9f - layer * 0.3f));
                    c.A = 0;
                    Vector2 drawPos = new Vector2(basePos.X + xOff, basePos.Y) - Main.screenPosition;
                    Main.spriteBatch.Draw(branch, drawPos, null, c, 0f,
                        new Vector2(branch.Width * 0.5f, branch.Height), // 原点=底部中心, 向上伸展
                        new Vector2(sx, ColumnHeight / branch.Height), SpriteEffects.None, 0f);
                }
            }

            float ringR = ACMUtils.QuadOut(st) * 120f * Scale;
            WeaponVFX.DrawShockwaveRing(basePos, 14f + ringR, 9f, fade * 0.9f,
                PillarPalette.HolyWhite, PillarPalette.SkyCyan);
            WeaponVFX.DrawGlowBurst(basePos, (2.4f * Scale) * (0.4f + 0.6f * fade), PillarPalette.Gold * fade);

            return false;
        }
    }

    /// <summary>
    /// 轰雷神铳 - 天柱敌怪掉落的手炮类远程武器。
    /// 机制身份: 雷霆后坐 + 六发转轮 — 每发强后坐可当位移用, 第六发轰天雷横排召落三道天罚。
    /// </summary>
    public class ThunderclapHandcannon : ModItem
    {
        private int roundCounter; // 转轮弹仓计数 (Shoot 仅 owner 端调用, 实例字段安全)

        public override void SetDefaults() {
            Item.damage = 150;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 44;
            Item.height = 28;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null; // 手动分层播放 (低频冲击 + 高频电裂)
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ThunderclapBlast>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Bullet;
            Item.crit = 10;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<ThunderclapBlast>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            roundCounter++;
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            bool thunderRound = roundCounter >= 6;

            if (thunderRound) {
                // —— 第六发·轰天雷 ——
                roundCounter = 0;
                Projectile.NewProjectile(source, position, velocity * 1.15f,
                    ModContent.ProjectileType<ThunderboltShell>(), (int)(damage * 2.2f), knockback * 1.6f, player.whoAmI);

                ApplyRecoil(player, dir, 11f, 20f); // 后坐 ×2.5, 向下射击可明显腾空
                WeaponVFX.AddScreenShake(player.Center, 4f);
                SoundEngine.PlaySound(SoundID.Item38 with { Pitch = -0.4f, Volume = 1f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.1f, Volume = 0.9f }, player.Center);

                for (int i = 0; i < 22; i++) {
                    Vector2 dustVel = dir.RotatedByRandom(0.45f) * Main.rand.NextFloat(5f, 14f);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.GoldFlame;
                    Dust d = Dust.NewDustPerfect(position + dir * 20f, dustType, dustVel, 80, default, 2.2f);
                    d.noGravity = true;
                }
            }
            else {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

                ApplyRecoil(player, dir, 4.5f, 12f);
                WeaponVFX.AddScreenShake(player.Center, 2f);
                // 转轮进膛音高递升 (可听计数)
                SoundEngine.PlaySound(SoundID.Item38 with { Pitch = -0.2f + roundCounter * 0.06f, Volume = 0.75f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 0.35f }, player.Center);

                for (int i = 0; i < 10; i++) {
                    Vector2 dustVel = dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(4f, 9f);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.GoldFlame;
                    Dust d = Dust.NewDustPerfect(position + dir * 16f, dustType, dustVel, 100, default, 1.6f);
                    d.noGravity = true;
                }
                // 尾烟后喷 (质量=反作用)
                for (int i = 0; i < 6; i++) {
                    Vector2 dustVel = -dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f);
                    Dust d = Dust.NewDustPerfect(position, DustID.Smoke, dustVel, 140, default, 1.1f);
                    d.noGravity = true;
                }
            }

            return false;
        }

        /// <summary>后坐冲量: 沿射向反方向, 已有后退速度超过 cap 时不再叠加 (防连射无限加速)。</summary>
        private static void ApplyRecoil(Player player, Vector2 dir, float impulse, float cap) {
            float backSpeed = Vector2.Dot(player.velocity, -dir);
            if (backSpeed < cap)
                player.velocity -= dir * impulse;
        }

        public override void HoldItem(Player player) {
            // 第五发后: 枪身缠绕电弧提示轰天雷已上膛
            if (roundCounter == 5 && !Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 muzzle = player.Center + new Vector2(player.direction * 26f, -4f);
                Dust d = Dust.NewDustPerfect(muzzle + Main.rand.NextVector2Circular(10f, 7f), DustID.Electric,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 110, default, 0.95f);
                d.noGravity = true;
            }
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-8, 0);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "凝聚天雷之力的神圣手炮，后坐力足以推动持有者"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "雷霆弹命中引发连锁闪电"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "六发转轮：第六发为轰天雷，横排轰落三道天罚落雷"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 雷霆弹 - 转轮 1~5 发, 命中后引发连锁闪电
    /// </summary>
    public class ThunderclapBlast : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f, Volume = 0.6f }, Projectile.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1f, Projectile.owner);

            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 寻找附近敌人释放闪电链
            int chainCount = 0;
            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.whoAmI == target.whoAmI) continue;
                float dist = Vector2.Distance(target.Center, npc.Center);
                if (dist < 300f && chainCount < 3) {
                    chainCount++;
                    Vector2 direction = (npc.Center - target.Center).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, direction * 12f,
                        ModContent.ProjectileType<ChainLightning>(), Projectile.damage / 2, 1f, Projectile.owner, npc.whoAmI);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 雷霆弹金白祥瑞双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(Color.Cyan, Color.Gold, progress);
                trailColor *= progress * 0.6f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * (0.5f + progress * 0.5f), SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Gold * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 轰天雷 - 转轮第六发。巨型雷弹, 首个命中/落地点横排轰落三道天罚落雷。
    /// </summary>
    public class ThunderboltShell : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        private bool BoltsFired {
            get => Projectile.ai[1] > 0f;
            set => Projectile.ai[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            for (int i = 0; i < 2; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), dustType,
                    -Projectile.velocity * 0.08f, 90, default, 1.7f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, PillarPalette.Lightning.ToVector3() * 0.8f);
        }

        private void FireBolts(Vector2 center) {
            if (BoltsFired)
                return;
            BoltsFired = true;
            int boltDamage = (int)(Projectile.damage * 0.3f); // ≈0.65× 武器伤害/道
            for (int i = -1; i <= 1; i++) {
                HeavenJudgmentBolt.Strike(Projectile.GetSource_FromThis(),
                    center + new Vector2(i * 90f, 0f), boltDamage, 2f, Projectile.owner, 0.9f);
            }
            WeaponVFX.AddScreenShake(center, 4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.4f, Projectile.owner);
            FireBolts(target.Center);

            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 70, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            // 撞地未命中敌人 → 落点仍轰一排天罚 (兑现第六发承诺)
            FireBolts(Projectile.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Electric;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 轰天雷: 加宽 ribbon + 雷光轴线
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
                outerColor: new Color(140, 215, 235, 140), innerColor: new Color(255, 250, 220, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - axis * 90f, Projectile.Center + axis * 22f, 7f,
                PillarPalette.Lightning, PillarPalette.DeepAzure, 0.7f, flowSpeed: 3f, coreSharp: 2.6f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            Color glowColor = PillarPalette.Gold * 0.7f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 2.2f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 连锁闪电 - 手炮命中后的闪电链
    /// </summary>
    public class ChainLightning : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 11;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 追踪目标
            int targetId = (int)TargetIndex;
            if (targetId >= 0 && targetId < Main.npc.Length && Main.npc[targetId].active) {
                Vector2 toTarget = (Main.npc[targetId].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 15f, 0.15f);
            }

            int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.GoldFlame;
            int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.3f);
            Main.dust[dust].noGravity = true;

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.9f, 1f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.5f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 连锁闪电: 向追踪目标拉一道青白电弧 (BeamGrad)
            int tgt = (int)TargetIndex;
            if (tgt >= 0 && tgt < Main.npc.Length && Main.npc[tgt].active)
                ACMShaders.DrawBeam(Projectile.Center, Main.npc[tgt].Center, 5f,
                    new Color(220, 245, 255), new Color(120, 200, 255), 0.8f, flowSpeed: 2.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(Color.Gold, Color.Cyan, 1f - progress);
                trailColor *= progress * 0.7f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 80, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

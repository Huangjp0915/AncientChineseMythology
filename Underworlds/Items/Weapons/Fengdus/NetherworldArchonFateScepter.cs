using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 冥府至尊判官司命杖 - 终极魔法权杖
    /// 左键: 双螺旋朱笔灵球, 命中施加/叠加"命运烙印" (上限 6 层, 记录期间伤害)
    /// 右键: 司命判决 - 对场上所有烙印目标降下帝诏, 以累积伤害 ×(1.2+0.15×层数) 勾决
    /// 烙印 3 秒无人判决则自动引爆兜底 (累积 ×1.5)
    /// </summary>
    public class NetherworldArchonFateScepter : ModItem
    {
        public override void SetDefaults() {
            // 发数 5→2 换单发 3800→8000: 5×3800 ≈ 2×8000×1.2 判决循环, DPS 持平但节奏可读
            Item.damage = 8000;
            Item.crit = 18;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 15;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<FateDecreeOrb>();
            Item.shootSpeed = 16f;
            Item.staff[Item.type] = true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 右键判决: 节奏放缓, 且须场上有己方烙印可判
                Item.useTime = 30;
                Item.useAnimation = 30;
                Item.UseSound = SoundID.Item117 with { Volume = 0.9f, Pitch = -0.3f };
                return player.ownedProjectileCounts[ModContent.ProjectileType<FateMarkProj>()] > 0;
            }
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.UseSound = SoundID.Item8;
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                // 司命判决: 每枚烙印换发一道"判决执行" (帝诏演出+勾决斩); 烙印被消费, 提前 Kill 不触发自爆
                int markType = ModContent.ProjectileType<FateMarkProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != markType || p.owner != player.whoAmI)
                        continue;
                    Projectile.NewProjectile(source, p.Center, Vector2.Zero,
                        ModContent.ProjectileType<FateVerdictFlash>(), damage, 0f, player.whoAmI,
                        p.ai[0], p.ai[1], p.ai[2]);
                    p.Kill();
                }
                return false;
            }

            // 左键: 镜像双发 (ai[1]=±1 螺旋相位符号), 出膛点沿垂直方向微分离
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = aim.RotatedBy(MathHelper.PiOver2);
            for (int s = -1; s <= 1; s += 2) {
                Vector2 spawnPos = player.Center + aim * 30f + perp * 6f * s;
                Projectile.NewProjectile(source, spawnPos, velocity, type, damage, knockback, player.whoAmI, 0f, s);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StaveofNetherEclipse>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 命运法令灵球 - 朱笔灵球, 双螺旋直飞 (ai[1]=±1 相位), 弱追踪保证轨迹可读。
    /// </summary>
    public class FateDecreeOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NetherworldArchonFateScepter";

        private ref float Timer => ref Projectile.ai[0];
        private ref float HelixSign => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.12f;

            // 弱追踪 (lerp 0.05): 轨迹以直线+螺旋为主, 追踪只作微修正
            if (Timer > 10f) {
                NPC target = FindTarget(1200f);
                if (target != null) {
                    Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 22f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.05f);
                }
            }

            if (Projectile.velocity.Length() > 24f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 24f;

            // 直飞基础上叠加垂直正弦摆动, 两枚 ±相位镜像成双螺旋
            Vector2 wavePerp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.position += wavePerp * (MathF.Sin(Timer * 0.25f) * 4.5f * HelixSign);

            // 朱紫拖尾 + 金红点缀
            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(6, 6),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust accent = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10), 4, 4,
                    Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch, 0f, -1.5f, 100, default, 1.5f);
                accent.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.55f, 0.22f, 0.65f);
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float best = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < best) { best = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.BrokenArmor, 600);
            target.AddBuff(BuffID.Ichor, 600);

            // 命运烙印: 已有则叠层 (上限 6) 并刷新计时, 否则新印 1 层起
            bool alreadyMarked = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<FateMarkProj>()
                    && p.owner == Projectile.owner && (int)p.ai[0] == target.whoAmI) {
                    alreadyMarked = true;
                    p.ai[2] = Math.Min(p.ai[2] + 1f, 6f);
                    p.ai[1] += damageDone;
                    p.timeLeft = 180;
                    p.netUpdate = true;
                    break;
                }
            }
            if (!alreadyMarked) {
                int markType = ModContent.ProjectileType<FateMarkProj>();
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    markType, Projectile.damage, 0f, Projectile.owner, target.whoAmI, damageDone, 1f);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 1f, Projectile.owner);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center,
                    Main.rand.NextBool() ? DustID.PurpleTorch : DustID.RedTorch, vel, 60, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 朱笔灵球: 外虚空黑紫 + 内帝金 ribbon 拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, 14f,
                FengduVFX.VoidDark * 0.95f, FengduVFX.ImperialGoldHi,
                ACMAsset.SoftGlow, uvScroll: 0.05f, subdivisions: 2);

            // 弹头双层辉光: 朱红小芯 + 紫外晕
            float pulse = 0.7f + MathF.Sin(Timer * 0.3f) * 0.15f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, pulse * 0.8f, FengduVFX.LethalRed * 0.85f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, pulse * 1.6f, FengduVFX.VoidMid * 0.4f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 80, default, 2f);
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 命运烙印 - 附着在敌人身上的持久烙印。
    /// ai[0]=目标 NPC id, ai[1]=累积伤害, ai[2]=层数 (1~6, 绕印小符数量=层数)。
    /// 3 秒无人判决则自动引爆兜底 (累积 ×1.5); 被右键判决消费时提前 Kill 不重复结算。
    /// </summary>
    public class FateMarkProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NetherworldArchonFateScepter";

        private ref float TargetNPC => ref Projectile.ai[0];
        private ref float AccumulatedDamage => ref Projectile.ai[1];
        private ref float Stacks => ref Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];
        private const int DetonationTime = 180; // 3 seconds

        // 同屏 ArenaRunic 司命烙印地纹每帧仅一枚承担 (按敌人数增殖时不叠 N 张全屏 SDF)
        private static ulong _lastRunicFrame;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DetonationTime;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;
        public override bool? CanCutTiles() => false;

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center + new Vector2(0, -target.height * 0.7f);
            Projectile.rotation += 0.05f;

            float progress = Timer / DetonationTime;
            float opacity = MathHelper.Clamp(Timer / 15f, 0f, 1f);

            // 倒计时向心紫尘 (随引爆临近增多, ≤6/帧)
            int particleCount = 1 + (int)(progress * 4f);
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(15f, 35f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
                Dust countdown = Dust.NewDustPerfect(pos, DustID.PurpleTorch, vel, 60,
                    default, Main.rand.NextFloat(1f, 2f) * opacity);
                countdown.noGravity = true;
            }

            if (Main.rand.NextBool(3)) {
                Dust gold = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(1f, 2f)), 80, default, 1.5f * opacity);
                gold.noGravity = true;
            }

            float lightIntensity = 0.3f + progress * 0.7f;
            Lighting.AddLight(Projectile.Center, 0.5f * lightIntensity, 0.25f * lightIntensity, 0.7f * lightIntensity);
        }

        public override void OnKill(int timeLeft) {
            // 提前 Kill (被判决消费/目标消失) 不走兜底引爆; 仅自然到时结算
            if (timeLeft > 0) return;

            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs) return;
            NPC target = Main.npc[targetIdx];
            if (!target.active) return;

            int bonusDamage = (int)(AccumulatedDamage * 1.5f);
            if (bonusDamage < Projectile.damage) bonusDamage = Projectile.damage * 3;

            // 伤害结算仅 owner 端 (多人安全)
            if (Main.myPlayer == Projectile.owner) {
                target.SimpleStrikeNPC(bonusDamage, 0, false, 0f, null, false, 0, true);

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == targetIdx) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 300f) {
                        nearby.SimpleStrikeNPC(bonusDamage / 2, 0, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.BrokenArmor, 300);
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.3f }, target.Center);

            // 命运回响引爆: 虚空审判 Burst + 震屏
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), target.Center, ACMWeaponBurst.FengduVoid, 2.4f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 6f);

            // 引爆粒子: 紫环 18 + 金屑 12 + 朱红竖柱 10 (一次性 ≤40)
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi / 18f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 18f);
                Dust ring = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 40, default, Main.rand.NextFloat(2.5f, 4f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                burst.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Dust pillar = Dust.NewDustPerfect(target.Center,
                    DustID.RedTorch, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(8f, 20f)),
                    40, default, Main.rand.NextFloat(2f, 3.5f));
                pillar.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / DetonationTime;
            float opacity = MathHelper.Clamp(Timer / 15f, 0f, 1f);

            // 命运烙印: ArenaRunic 司命符环地纹 (金紫, 随引爆临近收紧/加浓)
            // 多敌被标记时该弹按数量增殖, 全屏 SDF 每帧仅一枚承担; 其余退化为廉价符环
            Effect runic = ACMShaders.ArenaRunic;
            if (runic != null && _lastRunicFrame != Main.GameUpdateCount) {
                _lastRunicFrame = Main.GameUpdateCount;
                float runeRadius = MathHelper.Lerp(70f, 44f, progress); // 临近引爆向心收口
                ACMShaders.WorldDecalParams(Projectile.Center, runeRadius, out Vector2 uv, out float rFrac, out float aspect);
                runic.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                runic.Parameters["uCenter"]?.SetValue(uv);
                runic.Parameters["uRadius"]?.SetValue(rFrac);
                runic.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp((0.45f + progress * 0.55f) * opacity, 0f, 1f));
                runic.Parameters["uAspect"]?.SetValue(aspect);
                runic.Parameters["uColorPrimary"]?.SetValue(FengduVFX.VoidMid.ToVector4());
                runic.Parameters["uColorSecondary"]?.SetValue(FengduVFX.ImperialGold.ToVector4());
                runic.Parameters["uRuneFreq"]?.SetValue(11f);
                runic.Parameters["uMode"]?.SetValue(0f);  // 法阵符环
                runic.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, runic);
            }
            else {
                // 退化廉价符环 (BlankStar 旋转符印, 不占全屏 decal)
                Texture2D star = ACMAsset.BlankStar;
                if (star != null) {
                    Vector2 starOrigin = star.Size() / 2f;
                    float ringScale = MathHelper.Lerp(0.5f, 0.32f, progress);
                    Color ringTint = FengduVFX.VoidMid * opacity * (0.45f + progress * 0.4f);
                    ringTint.A = 0;
                    Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, ringTint,
                        Timer * 0.06f, starOrigin, ringScale, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, ringTint * 0.6f,
                        -Timer * 0.04f, starOrigin, ringScale * 1.4f, SpriteEffects.None, 0);
                }
            }

            // 层数可读: 绕印金色小符 (数量 = 层数, 第 i 枚相位 TwoPi*i/层数)
            Texture2D glyphStar = ACMAsset.BlankStar;
            int stacks = (int)Stacks;
            if (glyphStar != null && stacks > 0) {
                Vector2 glyphOrigin = glyphStar.Size() / 2f;
                Color glyphTint = FengduVFX.ImperialGold * opacity * 0.85f;
                glyphTint.A = 0;
                for (int i = 0; i < stacks; i++) {
                    float ang = Timer * 0.07f + MathHelper.TwoPi * i / stacks;
                    Vector2 glyphPos = Projectile.Center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 40f;
                    Main.EntitySpriteDraw(glyphStar, glyphPos - Main.screenPosition, null, glyphTint,
                        ang, glyphOrigin, 0.09f, SpriteEffects.None, 0);
                }
            }

            // SoftGlow aura (紫底 + 引爆临近金环)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                float auraSize = 0.8f + progress * 0.5f + MathF.Sin(Timer * 0.15f) * 0.2f;
                Color auraColor = FengduVFX.VoidMid * opacity * 0.35f;
                auraColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, auraColor,
                    0f, origin, auraSize, SpriteEffects.None, 0);

                Color ringColor = FengduVFX.ImperialGold * opacity * progress * 0.5f;
                ringColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, ringColor,
                    0f, origin, auraSize * 1.5f, SpriteEffects.None, 0);
            }

            // Sparkle overlay near detonation
            if (progress > 0.6f) {
                Texture2D sparkle = ACMAsset.Sparkle;
                if (sparkle != null) {
                    Vector2 origin = sparkle.Size() / 2f;
                    float sparkleOpacity = (progress - 0.6f) / 0.4f;
                    Color sparkleColor = new Color(255, 220, 120) * sparkleOpacity * opacity * 0.4f;
                    sparkleColor.A = 0;
                    float scale = 0.1f + sparkleOpacity * 0.1f;
                    Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkleColor,
                        Timer * 0.1f, origin, scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 司命判决执行 (演出+结算一体, 每个烙印目标一枚)。
    /// ai[0]=目标 NPC id, ai[1]=累积伤害, ai[2]=烙印层数。
    /// 0-22 帧帝诏自头顶展开 → 22-30 帧顿帧符亮 → 第 30 帧勾决斩 → 30-46 帧收卷淡出。
    /// </summary>
    public class FateVerdictFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private ref float TargetNPC => ref Projectile.ai[0];
        private ref float AccumulatedDamage => ref Projectile.ai[1];
        private ref float Stacks => ref Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        private const int Life = 46;
        private const int UnrollEnd = 22;
        private const int StrikeFrame = 30;
        private const float BandLength = 260f;

        // 金紫定调 palette tint 每帧只跑一枚 (多目标同时判决防全屏叠加)
        private static ulong _tintFrame;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        public override void AI() {
            Timer++;

            // 跟随目标 (目标消失则驻留原地完成演出)
            int idx = (int)TargetNPC;
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active)
                Projectile.Center = Main.npc[idx].Center;

            if ((int)Timer == UnrollEnd)
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);

            if ((int)Timer == StrikeFrame)
                ExecuteVerdict();

            // 顿帧段沿诏书带落下的少量金尘 (悬念)
            if (Timer >= UnrollEnd && Timer < StrikeFrame && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -Main.rand.NextFloat(0f, BandLength)),
                    DustID.GoldFlame, new Vector2(0f, 1.2f), 100, default, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.35f, 0.7f);
        }

        private void ExecuteVerdict() {
            // 勾决斩: 累积伤害 × (1.2 + 0.15×层数), 满 6 层 ×2.1; 仅 owner 端结算
            int idx = (int)TargetNPC;
            if (Main.myPlayer == Projectile.owner && idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                float mult = 1.2f + 0.15f * MathHelper.Clamp(Stacks, 1f, 6f);
                int strike = Math.Max((int)(AccumulatedDamage * mult), Projectile.damage);
                Main.npc[idx].SimpleStrikeNPC(strike, 0, false, 0f, null, false, 0, true);
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = -0.35f }, Projectile.Center);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.LethalRed, 1.8f, Projectile.owner);
            WeaponVFX.AddScreenShake(Projectile.Center, 4f);

            // 勾决一次性粒子: 朱红环 16 + 金屑 10 (≤40)
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi / 16f * i;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * Main.rand.NextFloat(7f, 14f),
                    40, default, Main.rand.NextFloat(2.2f, 3.4f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Dust fleck = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(9f, 9f), 60, default, Main.rand.NextFloat(1.6f, 2.6f));
                fleck.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Timer;

            // 判决开场金紫定调 (前 15 帧渐落; 静态帧限流, 多枚同帧只跑一枚)
            if (t <= 15f && _tintFrame != Main.GameUpdateCount) {
                _tintFrame = Main.GameUpdateCount;
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch, FengduVFX.VoidDark, FengduVFX.ImperialGoldHi,
                    0.12f * (1f - t / 15f), saturation: 1.05f, hueShift: 0f);
            }

            // 帝诏卷轴: 头顶 260px 卷首向下展开 → 顿帧全展 → 判决后反向收卷
            float unroll;
            float intensity;
            if (t < UnrollEnd) {
                unroll = t / UnrollEnd;
                intensity = 0.85f;
            }
            else if (t < StrikeFrame) {
                unroll = 1f;
                intensity = 1f; // 顿帧: 符文最亮
            }
            else {
                float f = MathHelper.Clamp((t - StrikeFrame) / (Life - StrikeFrame), 0f, 1f);
                unroll = 1f - f;
                intensity = 0.85f * (1f - f);
            }
            Vector2 top = Projectile.Center + new Vector2(0f, -BandLength);
            FengduVFX.DrawDecreeBand(top, Projectile.Center, 13f, unroll, intensity,
                glyphFreq: 10f, seed: Projectile.whoAmI * 0.31f);

            // 展开前沿的金光游标
            if (t < UnrollEnd) {
                Vector2 tip = Vector2.Lerp(top, Projectile.Center, unroll);
                WeaponVFX.DrawGlowBurst(tip, 0.5f, FengduVFX.ImperialGoldHi * 0.7f);
            }

            // 勾决斩冲击环 (ease-out 扩张)
            if (t >= StrikeFrame) {
                float w = MathHelper.Clamp((t - StrikeFrame) / 12f, 0f, 1f);
                float r = MathHelper.Lerp(24f, 190f, 1f - (1f - w) * (1f - w));
                WeaponVFX.DrawShockwaveRing(Projectile.Center, r, 13f, (1f - w) * 0.85f,
                    FengduVFX.LethalRed, FengduVFX.VoidDark);
            }
            return false;
        }
    }
}

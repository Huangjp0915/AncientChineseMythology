using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 黄泉万劫引魂弓 - UnderworldSoulguide的觉醒升级版
    /// 每次射出 3 支追踪引魂箭; 命中同一目标叠"引魂印", 6 印时该目标头顶降下
    /// 黄泉引渡柱 (魂柱贯穿 + 击飞) — 集火即是引渡。
    /// 觉醒形态: 每箭命中分裂 2 支渡魂小箭 (0.4×, 追踪)。
    /// </summary>
    public class DamnedSoulguide : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 900;
            Item.crit = 20;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 32;
            Item.height = 72;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override Vector2? HoldoutOffset() { return new Vector2(-2, 0); }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int damnedArrow = ModContent.ProjectileType<DamnedSoulguideArrow>();
            // 三支引魂箭 (紧凑扇形, 伤害集中)
            for (int i = -1; i <= 1; i++) {
                Vector2 perturbedSpeed = velocity.RotatedBy(MathHelper.ToRadians(i * 4f)) * Main.rand.NextFloat(0.95f, 1.05f);
                Projectile.NewProjectile(source, position, perturbedSpeed, damnedArrow, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<UnderworldSoulguide>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 万劫引魂箭 (ai[1]=1 为觉醒渡魂小箭): 强追踪; 命中叠引魂印 (owner 侧记层),
    /// 6 印降下黄泉引渡柱。
    /// </summary>
    public class DamnedSoulguideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/DamnedSoulguide";
        private ref float HomingTimer => ref Projectile.ai[0];
        private bool Mini => Projectile.ai[1] >= 1f;
        private const int MarksForPillar = 6;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            HomingTimer++;

            if (HomingTimer > (Mini ? 4f : 8f)) {
                NPC target = FindClosestNPC(Mini ? 500f : 800f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), Mini ? 0.12f : 0.08f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 1f, 1.4f);

            // 幽魂尾迹 (小箭减半)
            if (!Mini || Main.rand.NextBool(2)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f, 4, 4, DustID.Wraith,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f));
                soul.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust glow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8), 2, 2, DustID.BlueTorch,
                    0f, -0.5f, 80, default, 1.1f);
                glow.noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) { closestDist = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);

            for (int i = 0; i < (Mini ? 6 : 12); i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -2f));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.5f, 2.4f));
                soul.noGravity = true;
            }

            if (Projectile.owner != Main.myPlayer)
                return;

            Player owner = Main.player[Projectile.owner];
            var mp = owner.GetModPlayer<RevenantEXKarmaPlayer>();
            mp.AddKarma(Mini ? 0.4f : 0.8f);

            // 目标死亡 → 清印 (防 whoAmI 槽位复用继承旧层数)
            if (target.life <= 0) {
                mp.SoulMarks[target.whoAmI] = 0;
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.SoulFire, scale: 1.2f, owner: Projectile.owner);
            }
            // —— 引魂印: 同一目标集火叠层, 6 印降柱 ——
            else if (!Mini && target.active) {
                int idx = target.whoAmI;
                mp.SoulMarks[idx]++;
                // 印记视觉: 层数越高魂环越大
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 0.7f + mp.SoulMarks[idx] * 0.1f, owner: Projectile.owner);
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.4f, Pitch = -0.3f + mp.SoulMarks[idx] * 0.1f }, target.Center);

                if (mp.SoulMarks[idx] >= MarksForPillar) {
                    mp.SoulMarks[idx] = 0;
                    // 黄泉引渡柱: 从天而降贯穿魂柱 (2.4×, 击飞)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target),
                        new Vector2(target.Center.X, target.Center.Y), Vector2.Zero,
                        ModContent.ProjectileType<YomiPillarStrike>(),
                        (int)(Projectile.damage * 2.4f), 12f, Projectile.owner, target.whoAmI);
                    mp.AddKarma(5f);
                }
            }
            else {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: Mini ? 0.6f : 1f, owner: Projectile.owner);
            }

            // 觉醒形态: 命中分裂 2 支渡魂小箭 (0.4×)
            if (!Mini && mp.Awakened) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy((i == 0 ? 1f : -1f) * 0.9f);
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center + dir * 20f, dir * 13f,
                        ModContent.ProjectileType<DamnedSoulguideArrow>(),
                        (int)(Projectile.damage * 0.4f), Projectile.knockBack * 0.3f, Projectile.owner, 0f, 1f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 蓝魂双层带状箭迹
            WeaponVFX.DrawProjectileTrail(Projectile, Mini ? 9f : 14f,
                new Color(30, 70, 190), new Color(150, 220, 255),
                uvScroll: HomingTimer * 0.03f);

            Texture2D softGlow = ACMAsset.SoftGlow;
            Texture2D blankStar = ACMAsset.BlankStar;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = new Color(120, 200, 255) * 0.8f;
                mainGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, Mini ? 0.6f : 1f, SpriteEffects.None, 0);
            }
            if (blankStar != null && !Mini) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.4f + MathF.Sin(HomingTimer * 0.25f) * 0.12f;
                Color starColor = new Color(180, 240, 255) * 0.7f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, HomingTimer * 0.15f, starOrigin, pulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Wraith,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -1f),
                    80, default, Main.rand.NextFloat(1.3f, 2f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 黄泉引渡柱 (6 引魂印大招): 目标头顶降下贯穿魂柱, 2.4× 一击 + 击飞;
    /// BeamGrad 双层魂柱 + 落点冲击环。ai[0]=目标 whoAmI (贴附目标位置)。
    /// </summary>
    public class YomiPillarStrike : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float TargetIdx => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private const int Windup = 14;    // 预警帧 (柱影收束)
        private const int StrikeLife = 26;
        private const float PillarHeight = 640f;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.YomiPillarStrike.DisplayName",
                () => "Yellow Springs Ferry Pillar");
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Windup + StrikeLife;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每目标一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            // 贴附目标 (存活时)
            int idx = (int)TargetIdx;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC t = Main.npc[idx];
                if (t.active && t.CanBeChasedBy())
                    Projectile.Center = t.Center;
            }

            if (Timer < Windup) {
                // 预警: 天光收束
                if (Main.rand.NextBool(2)) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -Main.rand.NextFloat(200f, PillarHeight));
                    Dust converge = Dust.NewDustPerfect(pos, DustID.BlueTorch,
                        new Vector2(0f, Main.rand.NextFloat(8f, 16f)), 80, default, Main.rand.NextFloat(1.4f, 2f));
                    converge.noGravity = true;
                }
                return;
            }
            if ((int)Timer == Windup) {
                // 落柱帧: 重音 + 震屏 + 魂爆 + 蓝魂连锁网 (演出)
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 6f);
                DamnedSoulChain.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner);
                for (int i = 0; i < 26; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-14f, -4f));
                    Dust soul = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 70, default, Main.rand.NextFloat(2f, 3.2f));
                    soul.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.5f, 1f, 1.6f);
        }

        // 只在落柱后有伤害; 判定为竖直柱体
        public override bool? CanDamage() => Timer >= Windup;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 bottom = Projectile.Center + new Vector2(0f, 60f);
            Vector2 top = Projectile.Center - new Vector2(0f, PillarHeight);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), top, bottom, 46f, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.Knockback += 4f;
            modifiers.HitDirectionOverride = 0; // 垂直击飞
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 420);
            target.AddBuff(BuffID.Frostburn2, 420);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 1.6f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 bottom = Projectile.Center + new Vector2(0f, 50f);
            Vector2 top = Projectile.Center - new Vector2(0f, PillarHeight);

            if (Timer < Windup) {
                // 预警细光柱 (公平阀: 先看到再挨打)
                float warn = Timer / (float)Windup;
                ACMShaders.DrawBeam(top, bottom, 5f + warn * 7f,
                    new Color(150, 220, 255), new Color(30, 90, 200), 0.35f + warn * 0.3f,
                    flowSpeed: 4f, flowScale: 2.4f, coreSharp: 2f);
                return false;
            }

            float life = (Timer - Windup) / StrikeLife;
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);

            // 双层魂柱: 宽暗底 + 亮芯
            ACMShaders.DrawBeam(top, bottom, 54f * fade,
                new Color(90, 170, 255), new Color(20, 50, 150), fade * 0.7f,
                flowSpeed: 2.2f, flowScale: 2.2f, coreSharp: 1.8f);
            ACMShaders.DrawBeam(top, bottom, 22f * fade,
                new Color(230, 250, 255), new Color(90, 170, 255), fade,
                flowSpeed: 3.4f, flowScale: 2.8f, coreSharp: 3f);

            // 落点冲击环 + 辉光
            WeaponVFX.DrawShockwaveRing(bottom, 14f + life * 130f, 11f, fade * 0.85f,
                new Color(170, 230, 255), new Color(40, 80, 190));
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(bottom, 0.08f, fade * 0.65f, new Color(130, 200, 255), 6f);
            return false;
        }
    }

    /// <summary>
    /// 万劫连锁演出弹幕 (纯视觉, damage=0): 保留类 (旧暴击连锁演出), 现由黄泉引渡柱路径复用:
    /// 从命中点向周围敌人拉出 BeamGrad 蓝魂多线, 加冲击环 + 径向辉光。绘制只在 PreDraw。
    /// </summary>
    public class DamnedSoulChain : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 32;
        private const float ChainRange = 360f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<DamnedSoulChain>(), 0, 0f, owner);
        }

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
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);

            int web = 0;
            for (int i = 0; i < Main.maxNPCs && web < 6; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;
                if (Vector2.Distance(Projectile.Center, npc.Center) > ChainRange)
                    continue;
                ACMShaders.DrawBeam(Projectile.Center, npc.Center, 6f * fade,
                    new Color(170, 225, 255), new Color(40, 90, 220), fade * 0.9f,
                    flowSpeed: 2.6f, flowScale: 2.6f);
                web++;
            }

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 12f + life * 90f, 9f, fade * 0.8f,
                new Color(160, 220, 255), new Color(40, 80, 180));
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.07f, fade * 0.6f, new Color(120, 200, 255), 8f);

            return false;
        }
    }
}

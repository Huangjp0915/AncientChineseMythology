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
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 黄泉万劫引魂弓 - UnderworldSoulguide的终极升级版
    /// 引渡遭受万劫不复之苦的灵魂，箭矢具有极强追踪能力
    /// 特殊机制：每次射出5支追踪引魂箭，命中后灵魂连锁爆发
    /// </summary>
    public class DamnedSoulguide : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 480;
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
            // 发射5支万劫引魂箭
            for (int i = 0; i < 5; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(8));
                perturbedSpeed *= Main.rand.NextFloat(0.9f, 1.15f);
                Projectile.NewProjectile(source, position, perturbedSpeed, damnedArrow, damage, knockback, player.whoAmI);
            }
            // 额外概率射出灵魂箭
            if (Main.rand.NextBool(2)) {
                for (int i = 0; i < 3; i++) {
                    Vector2 bonusVel = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                    Projectile.NewProjectile(source, position, bonusVel * 0.85f, damnedArrow, (int)(damage * 0.7f), knockback, player.whoAmI);
                }
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

    public class DamnedSoulguideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/DamnedSoulguide";
        private ref float HomingTimer => ref Projectile.ai[0];

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
            Projectile.penetrate = 4;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            HomingTimer++;

            // 更强的追踪能力，更快启动
            if (HomingTimer > 8f) {
                NPC target = FindClosestNPC(800f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.08f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 1f, 1.4f);

            for (int i = 0; i < 2; i++) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f, 4, 4, DustID.Wraith,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.4f, 2f)
                );
                soul.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust glow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8), 2, 2, DustID.BlueTorch,
                    0f, -0.5f, 80, default, 1.2f
                );
                glow.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust phantom = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(12, 12), 4, 4, DustID.Shadowflame,
                    0f, -1f, 120, default, 1.5f
                );
                phantom.noGravity = true;
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

            // 灵魂连锁爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -2f));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                soul.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust star = Dust.NewDustPerfect(target.Center, DustID.BlueTorch, vel, 60, default, Main.rand.NextFloat(1.5f, 2.2f));
                star.noGravity = true;
            }

            // 万劫连锁：命中后对附近敌人也造成伤害
            if (hit.Crit) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 350f) {
                        nearby.SimpleStrikeNPC(damageDone / 3, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.Frostburn2, 180);
                        // 连锁视觉效果
                        for (int j = 0; j < 10; j++) {
                            float t = j / 10f;
                            Vector2 pos = Vector2.Lerp(target.Center, nearby.Center, t) + Main.rand.NextVector2Circular(5f, 5f);
                            Dust chain = Dust.NewDustPerfect(pos, DustID.BlueTorch, Vector2.Zero, 80, default, 1.2f);
                            chain.noGravity = true;
                        }
                    }
                }
                // 升级演出: 蓝魂多线连锁 (BeamGrad), 仅本机生成
                DamnedSoulChain.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 3f);
            }

            // 命中冲击演出 (径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.4f : 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 蓝魂双层带状箭迹 (外宽深蓝 + 内窄亮青)
            WeaponVFX.DrawProjectileTrail(Projectile, 14f,
                new Color(30, 70, 190), new Color(150, 220, 255),
                uvScroll: HomingTimer * 0.03f);

            Texture2D softGlow = ACMAsset.SoftGlow;
            Texture2D blankStar = ACMAsset.BlankStar;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = new Color(120, 200, 255) * 0.8f;
                mainGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, 1f, SpriteEffects.None, 0);
            }
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.4f + MathF.Sin(HomingTimer * 0.25f) * 0.12f;
                Color starColor = new Color(180, 240, 255) * 0.7f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, HomingTimer * 0.15f, starOrigin, pulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Wraith,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -1f),
                    80, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 万劫连锁演出弹幕 (纯视觉, damage=0): 暴击连锁瞬间从命中点向周围敌人拉出 BeamGrad 蓝魂多线,
    /// 加冲击环 + 径向辉光。绘制只在 PreDraw。
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

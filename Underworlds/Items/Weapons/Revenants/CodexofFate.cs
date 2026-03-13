using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 生死冥罗录 - 记载众生死期与因果的冥府秘典，魔法书类武器
    /// 肉后中期，释放命运符文弹幕，命中时召唤电弧链式打击
    /// </summary>
    public class CodexofFate : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 58;
            Item.crit = 6;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 10;
            Item.width = 36;
            Item.height = 36;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<FateRuneProjectile>();
            Item.shootSpeed = 14f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //释放2道命运符文
            for (int i = 0; i < 2; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(12));
                perturbedSpeed *= Main.rand.NextFloat(0.9f, 1.1f);
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }

            //施法时冥典翻页粒子
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust page = Dust.NewDustPerfect(
                    position, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(0.8f, 1.2f)
                );
                page.noGravity = true;
            }

            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 25f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 命运符文弹幕 - 飞行的冥府符文，命中敌人时产生电弧链式打击
    /// 使用ACMAsset.ElectricArcSheet叠加电弧效果，ACMAsset.LightningBranch绘制命中闪电
    /// </summary>
    public class FateRuneProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/CodexofFate";

        private ref float RotationTimer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            RotationTimer++;
            Projectile.rotation += 0.15f;

            //冥紫色光照
            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.6f);

            //微弱追踪
            if (RotationTimer > 15f) {
                NPC target = FindClosestNPC(350f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.03f);
                }
            }

            //符文粒子拖尾
            if (Main.rand.NextBool(2)) {
                Dust rune = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                rune.noGravity = true;
            }

            //偶尔产生电弧碎片
            if (Main.rand.NextBool(6)) {
                Dust arc = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                    4, 4, DustID.Electric,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f),
                    80, default, 0.7f
                );
                arc.noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命运裁决：附加多种减益
            target.AddBuff(BuffID.ShadowFlame, 120);
            target.AddBuff(BuffID.Electrified, 90);

            //命中时产生链式电弧打击特效
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust bolt = Dust.NewDustPerfect(
                    target.Center, DustID.Electric, vel,
                    80, default, Main.rand.NextFloat(1.0f, 1.6f)
                );
                bolt.noGravity = true;
            }

            //冥紫爆发
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                burst.noGravity = true;
            }

            //链式打击：对附近敌人造成额外伤害
            if (hit.Crit) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    float dist = Vector2.Distance(target.Center, nearby.Center);
                    if (dist < 200f) {
                        nearby.SimpleStrikeNPC(damageDone / 4, hit.HitDirection, false, 0f, null, false, 0, true);
                        //链式电弧粒子
                        Vector2 chainDir = (nearby.Center - target.Center).SafeNormalize(Vector2.Zero);
                        for (int j = 0; j < 8; j++) {
                            float t = j / 8f;
                            Vector2 pos = Vector2.Lerp(target.Center, nearby.Center, t);
                            pos += Main.rand.NextVector2Circular(4f, 4f);
                            Dust chain = Dust.NewDustPerfect(
                                pos, DustID.Electric,
                                chainDir.RotatedByRandom(0.3f) * 2f,
                                80, default, 0.9f
                            );
                            chain.noGravity = true;
                        }
                        break;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //使用ElectricArcSheet绘制符文电弧外圈
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                //取电弧纹理的一个区段（4组中随机一组）
                int arcIndex = (int)(RotationTimer * 0.1f) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

                Color arcColor = new Color(160, 100, 220) * 0.35f;
                arcColor.A = 0;
                float arcScale = 0.12f + MathF.Sin(RotationTimer * 0.2f) * 0.02f;
                Main.EntitySpriteDraw(arcSheet, Projectile.Center - Main.screenPosition, sourceRect, arcColor, Projectile.rotation + MathHelper.PiOver2, arcOrigin, arcScale, SpriteEffects.None, 0);
            }

            //使用SoftGlow绘制符文核心光球
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;

                //拖尾光球
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(80, 40, 140), new Color(180, 100, 255), progress) * progress * 0.4f;
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, drawPos, null, trailColor, 0f, glowOrigin, 0.4f * progress, SpriteEffects.None, 0);
                }

                //主体光球
                Color mainGlow = new Color(180, 100, 255) * 0.6f;
                mainGlow.A = 0;
                float pulse = 0.55f + MathF.Sin(RotationTimer * 0.2f) * 0.08f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            //核心星光
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(200, 150, 255) * 0.5f;
                starColor.A = 0;
                float starScale = 0.2f + MathF.Sin(RotationTimer * 0.3f) * 0.05f;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, RotationTimer * 0.15f, starOrigin, starScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f }, Projectile.Center);

            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }

            for (int i = 0; i < 4; i++) {
                Dust arc = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    80, default, 0.8f
                );
                arc.noGravity = true;
            }
        }
    }
}

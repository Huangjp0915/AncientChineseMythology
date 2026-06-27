using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 黄泉引魂弓 - 引渡灵魂前往黄泉的弓，远程弓类武器
    /// 肉后中期，发射的箭矢带有引魂追踪效果，命中后灵魂升腾
    /// </summary>
    public class UnderworldSoulguide : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 56;
            Item.crit = 8;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 24;
            Item.height = 58;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //将普通箭转换为引魂箭弹幕
            int soulArrow = ModContent.ProjectileType<SoulguideArrow>();
            Projectile.NewProjectile(source, position, velocity, soulArrow, damage, knockback, player.whoAmI);

            //有几率射出额外一支引魂箭
            if (Main.rand.NextBool(3)) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(10));
                Projectile.NewProjectile(source, position, perturbedSpeed * 0.9f, soulArrow, (int)(damage * 0.6f), knockback, player.whoAmI);
            }
            return false;
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
    /// 引魂箭弹幕 - 带有幽魂拖尾的追踪箭矢，命中后灵魂升腾
    /// 使用ACMAsset.SoftGlow和ACMAsset.BlankStar叠加绘制
    /// </summary>
    public class SoulguideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/UnderworldSoulguide";

        private ref float HomingTimer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            HomingTimer++;

            //飞行0.3秒后开始微弱追踪
            if (HomingTimer > 18f) {
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.04f);
                }
            }

            //幽蓝色光照
            Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.7f);

            //幽魂拖尾粒子
            if (Main.rand.NextBool(2)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    4, 4, DustID.Wraith,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    120, default, Main.rand.NextFloat(1.0f, 1.4f)
                );
                soul.noGravity = true;
            }

            //淡蓝光点
            if (Main.rand.NextBool(3)) {
                Dust glow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    2, 2, DustID.BlueTorch,
                    0f, -0.3f, 100, default, 0.7f
                );
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
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //引魂效果：灵魂升腾粒子
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
                Dust soul = Dust.NewDustPerfect(
                    target.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                soul.noGravity = true;
            }

            //蓝色星光爆发
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust star = Dust.NewDustPerfect(
                    target.Center, DustID.BlueTorch, vel,
                    80, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                star.noGravity = true;
            }

            //引魂"标记"演出: 命中点青黄魂火径向辉光 + 冲击环 (走 ACMWeaponBurst, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.SoulFire, scale: 0.8f, owner: Projectile.owner);

            target.AddBuff(BuffID.Frostburn, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            float brightness = MathHelper.Clamp(Projectile.timeLeft / 30f, 0.25f, 1f);

            //BeamGrad 蓝魂能量拖尾 (沿飞行方向的发光箭体光束)
            Vector2 head = Projectile.Center;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float beamLen = MathHelper.Clamp(Projectile.velocity.Length() * 3.2f, 36f, 130f);
            ACMShaders.DrawBeam(head - dir * beamLen, head + dir * 6f, halfWidth: 7f,
                core: new Color(170, 230, 255), edge: new Color(40, 90, 190),
                intensity: brightness, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

            //箭头核心径向辉光 (走全屏名额, 名额满自动退化为柔光)
            WeaponVFX.DrawRadialBloom(head, radiusFrac: 0.035f, intensity: brightness * 0.35f,
                color: new Color(140, 210, 255), rayCount: 6f);

            //箭尖星光闪烁
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = (0.28f + MathF.Sin(HomingTimer * 0.2f) * 0.08f) * brightness;
                Color starColor = new Color(160, 225, 255) * 0.5f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, head - Main.screenPosition, null, starColor, HomingTimer * 0.1f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消散时灵魂升腾
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Wraith,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }
        }
    }
}

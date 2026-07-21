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
    /// 流云落日弓 - 天柱敌怪掉落的弓类远程武器。
    /// 机制身份: 穿云 — 双箭连发数发, 每第五发凝成贯云神矢 (无限穿透巨矢),
    /// 首个命中目标头顶轰落天罚落雷。
    /// </summary>
    public class Cloudpiercer : ModItem
    {
        private int shotCounter; // 数发计数 (Shoot 仅 owner 端调用, 实例字段安全)

        public override void SetDefaults() {
            Item.damage = 185;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 24;
            Item.height = 56;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 12;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<CloudpiercerArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            shotCounter++;
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);

            if (shotCounter >= 5) {
                // —— 第五发·贯云神矢 ——
                shotCounter = 0;
                Projectile.NewProjectile(source, position, velocity * 1.25f,
                    ModContent.ProjectileType<PiercingSunArrow>(), (int)(damage * 2.2f), knockback * 1.5f, player.whoAmI);

                SoundEngine.PlaySound(SoundID.Item5 with { Pitch = -0.3f, Volume = 1f }, position);
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 0.8f }, position);
                WeaponVFX.AddScreenShake(player.Center, 2.5f);

                // 弓口金白光爆
                for (int i = 0; i < 16; i++) {
                    Vector2 dustVel = dir.RotatedByRandom(0.25f) * Main.rand.NextFloat(5f, 12f);
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                    Dust d = Dust.NewDustPerfect(position + dir * 24f, dustType, dustVel, 80, default, 2f);
                    d.noGravity = true;
                }
            }
            else {
                // 普通发: 双箭微扇 (3°), 比三箭更聚焦
                for (int i = 0; i < 2; i++) {
                    Vector2 newVel = velocity.RotatedBy(MathHelper.ToRadians(i == 0 ? -1.5f : 1.5f));
                    Projectile.NewProjectile(source, position, newVel, type, damage, knockback, player.whoAmI);
                }
                // 音高随数发递升 (可听计数)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.1f + shotCounter * 0.12f, Volume = 0.35f }, position);

                // 弓口计数环: 第 N 发喷 N 簇青尘 (可视计数)
                for (int i = 0; i < shotCounter * 2; i++) {
                    Vector2 dustVel = dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 4f);
                    Dust d = Dust.NewDustPerfect(position + dir * 20f, DustID.IceTorch, dustVel, 120, default, 1.2f);
                    d.noGravity = true;
                }
            }

            return false;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "以天柱精华淬炼的神弓"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "齐射两支追踪云隙的神圣箭矢"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "每第五发凝成无限贯穿的贯云神矢，首个命中者头顶轰落天罚落雷"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 穿云箭 - 金色青色追踪箭
    /// </summary>
    public class CloudpiercerArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JestersArrow;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微追踪 (8 帧重锁一次, 目标缓存在 localAI[0], 降低全表扫描频率)
            if (Projectile.timeLeft > 120) {
                if (Projectile.timeLeft % 8 == 0)
                    Projectile.localAI[0] = FindClosestNPC(500f) is NPC found ? found.whoAmI : -1f;

                int targetId = (int)Projectile.localAI[0];
                if (targetId >= 0 && targetId < Main.npc.Length && Main.npc[targetId].active && Main.npc[targetId].CanBeChasedBy()) {
                    Vector2 toTarget = (Main.npc[targetId].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.02f);
                }
            }

            // 金色+青色粒子拖尾
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center - Projectile.velocity * 0.5f, 0, 0, dustType, 0, 0, 150, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.4f) * 0.5f);
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
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.GoldFlame;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 穿云箭金白祥瑞双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 金色拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.6f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 主体发光
            Color glowColor = Color.Gold * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 贯云神矢 - 第五发凝聚的巨型光矢。无限贯穿直线, 首个命中目标头顶轰落天罚落雷。
    /// </summary>
    public class PiercingSunArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private bool BoltFired {
            get => Projectile.ai[1] > 0f;
            set => Projectile.ai[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 直线上每个敌人只命中一次
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 云隙尾流: 白金流尘 + 青雾
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), dustType,
                    -Projectile.velocity * 0.06f, 100, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, PillarPalette.HolyWhite.ToVector3() * 0.9f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.3f, Projectile.owner);

            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7, 7);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }

            // 首个命中者: 头顶天罚落雷 (系列高光收尾)
            if (!BoltFired) {
                BoltFired = true;
                HeavenJudgmentBolt.Strike(Projectile.GetSource_OnHit(target),
                    target.Center, (int)(Projectile.damage * 0.36f), 3f, Projectile.owner, 0.9f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 巨幅金白 ribbon + 沿飞行轴光矢
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 20f,
                outerColor: new Color(140, 215, 235, 150), innerColor: new Color(255, 252, 225, 210),
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);

            Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - axis * 130f, Projectile.Center + axis * 30f, 9f,
                PillarPalette.HolyWhite, PillarPalette.SkyCyan, 0.85f, flowSpeed: 3.2f, coreSharp: 2.8f, coreGlow: 0.8f);

            // 矢头光斑 (LightShot 朝向右, 转到飞行方向)
            Texture2D head = ACMAsset.LightShot;
            if (head != null) {
                Color c = PillarPalette.HolyWhite;
                c.A = 0;
                Main.spriteBatch.Draw(head, Projectile.Center - Main.screenPosition, null, c,
                    Projectile.velocity.ToRotation(), head.Size() * 0.5f, new Vector2(0.9f, 0.45f), SpriteEffects.None, 0f);
                Color halo = PillarPalette.Gold * 0.6f;
                halo.A = 0;
                Main.spriteBatch.Draw(head, Projectile.Center - Main.screenPosition, null, halo,
                    Projectile.velocity.ToRotation(), head.Size() * 0.5f, new Vector2(1.4f, 0.7f), SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

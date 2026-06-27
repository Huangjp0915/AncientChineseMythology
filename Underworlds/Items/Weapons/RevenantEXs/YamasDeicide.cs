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
    /// 阎摩断业屠神刀 - YamasSeverance的终极升级版
    /// 不仅裁断业报，更能屠戮神魔的巨刃
    /// 特殊机制：释放多道屠神斩击，暴击触发神魔审判，低血量非Boss敌人直接斩杀
    /// </summary>
    public class YamasDeicide : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1080;
            Item.crit = 25;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.scale = 1.6f;
            Item.shoot = ModContent.ProjectileType<YamasDeicideSlash>();
            Item.shootSpeed = 18f;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            for (int i = 0; i < 4; i++) {
                Dust flame = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Shadowflame,
                    player.velocity.X * 0.5f, player.velocity.Y * 0.5f,
                    80, default, 2.2f
                );
                flame.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                Dust soul = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith,
                    0f, -2f, 100, default, 1.8f
                );
                soul.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust divineFlame = Dust.NewDustDirect(
                    new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.PurpleTorch,
                    player.velocity.X * 0.4f, player.velocity.Y * 0.4f,
                    60, default, 2.5f
                );
                divineFlame.noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Ichor, 600);

            // 命中冲击演出 (暗冥紫径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.5f : 1f, owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, hit.Crit ? 4f : 2f);

            // 非Boss敌人血量低于15%时直接斩杀
            if (!target.boss && target.life < target.lifeMax * 0.15f) {
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.5f }, target.Center);
                for (int i = 0; i < 40; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
                    Dust kill = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 60, default, 3f);
                    kill.noGravity = true;
                }
                // 处决升级演出: 屠神溶解崩解 (DissolveBurn) + 致命纯红命中爆 (LethalRed), 仅本机生成
                YamaExecuteFinisher.Spawn(player.GetSource_OnHit(target), target.Center, target.width + target.height, player.whoAmI);
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.LethalRed, scale: 1.7f, owner: player.whoAmI);
                WeaponVFX.AddScreenShake(target.Center, 8f);
            }

            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    80, default, Main.rand.NextFloat(2.0f, 3.5f)
                );
                burst.noGravity = true;
            }

            if (hit.Crit) {
                // 神魔审判：暴击时对范围内所有敌人造成伤害
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.5f }, target.Center);
                for (int i = 0; i < 40; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(14f, 14f);
                    Dust ring = Dust.NewDustPerfect(
                        target.Center, DustID.PurpleTorch, vel,
                        60, default, Main.rand.NextFloat(2.5f, 4f)
                    );
                    ring.noGravity = true;
                }
                // 对附近敌人造成AOE伤害
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 400f) {
                        nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.ShadowFlame, 300);
                    }
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 释放3道屠神斩击
            for (int i = -1; i <= 1; i++) {
                Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(i * 8));
                Vector2 direction = perturbedVel.SafeNormalize(Vector2.UnitX);
                Vector2 spawnPos = player.Center + direction * 50f;
                Projectile.NewProjectile(source, spawnPos, perturbedVel, type, (int)(damage * 0.8f), knockback * 0.5f, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<YamasSeverance>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class YamasDeicideSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/YamasDeicide";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 8;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.alpha = 40;
        }

        public override void AI() {
            Projectile.alpha += 3;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 1f * brightness, 0.4f * brightness, 1.2f * brightness);

            for (int i = 0; i < 3; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust shard = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                    4, 4, DustID.PurpleTorch,
                    0f, -1f, 80, default, 1.5f
                );
                shard.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.OnFire3, 300);
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    80, default, Main.rand.NextFloat(1.8f, 2.8f)
                );
                burst.noGravity = true;
            }
            // 屠神斩波命中冲击演出 (暗冥紫)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = (255 - Projectile.alpha) / 255f;

            // 屠神斩 BeamGrad 主刃 (沿飞行轴的暗冥紫光束, 随消隐淡出)
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            ACMShaders.DrawBeam(Projectile.Center - dir * 70f, Projectile.Center + dir * 70f, 26f * opacity,
                new Color(235, 180, 255), new Color(120, 40, 200), opacity,
                flowSpeed: 2.0f, flowScale: 2.2f, coreSharp: 2.4f);

            // 双层带状斩迹 (外宽暗紫 + 内窄亮紫)
            WeaponVFX.DrawProjectileTrail(Projectile, 24f,
                new Color(120, 40, 200, 150), new Color(235, 190, 255, 200),
                uvScroll: -(float)Main.timeForVisualEffects * 0.03f);

            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate != null) {
                Vector2 origin = glaciate.Size() / 2f;
                Color mainColor = new Color(255, 150, 255) * opacity * 0.9f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, new Vector2(0.7f, 0.4f), SpriteEffects.None, 0);
                Color glowColor = new Color(200, 60, 255) * opacity * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, new Vector2(0.85f, 0.55f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 20; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 屠神处决演出弹幕 (纯视觉, damage=0): 斩杀低血敌人瞬间在其位置展开 DissolveBurn 溶解崩解斩痕
    /// (致命纯红灼边) + 冲击环 + 径向辉光。绘制只在 PreDraw, 命中阶段仅 <see cref="Spawn"/> 触发。
    /// </summary>
    public class YamaExecuteFinisher : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;
        private float Size => Projectile.ai[0] <= 0f ? 80f : Projectile.ai[0];

        public static void Spawn(IEntitySource source, Vector2 worldPos, float size, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<YamaExecuteFinisher>(), 0, 0f, owner, size);
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
            float fade = MathHelper.Clamp(1f - life, 0f, 1f);

            Texture2D slash = ACMAsset.SlashBurst;
            if (slash != null && slash.Width > 0) {
                Vector2 origin = slash.Size() * 0.5f;
                float scale = (Size / slash.Width) * MathHelper.Lerp(1.6f, 2.8f, life);
                WeaponVFX.ApplyDissolveBurn(slash, Projectile.Center, null,
                    new Color(250, 40, 56), 0f, origin, scale,
                    threshold: life, intensity: fade,
                    edgeColor: new Color(255, 140, 90, 255), edgeWidth: 0.11f, noiseScale: 2.6f);
            }

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 16f + life * 120f, 12f, fade * 0.85f,
                new Color(255, 110, 110), new Color(150, 12, 18));
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.08f, fade * 0.6f, new Color(250, 60, 70), 8f);

            return false;
        }
    }
}

using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses.Items
{
    /// <summary>
    /// 枉死千骸之书 - 继承Boss的传送拍掌攻击
    /// </summary>
    internal class CorpsesesBook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 6620;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CorpsesesGhostHand>();
            Item.shootSpeed = 0f;
            Item.mana = 20;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 在鼠标位置生成幽灵手
            Vector2 targetPos = Main.MouseWorld;

            // 左右手配对生成
            Vector2 leftHandPos = targetPos + new Vector2(-150, -100);
            Vector2 rightHandPos = targetPos + new Vector2(150, -100);

            Projectile.NewProjectile(source, leftHandPos, Vector2.Zero, type, damage, knockback, player.whoAmI, 0, -1);
            Projectile.NewProjectile(source, rightHandPos, Vector2.Zero, type, damage, knockback, player.whoAmI, 0, 1);

            ACMWeaponBurst.Spawn(source, targetPos, ACMWeaponBurst.GhostGreen, scale: 0.9f, owner: player.whoAmI);

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 幽灵手弹幕 - 模拟Boss的手部攻击
    /// </summary>
    public class CorpsesesGhostHand : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/Corpseses/CorpsesHand";
        private enum HandPhase
        {
            Appearing,   // 出现
            Charging,    // 蓄力
            Clapping,    // 拍击
            Dissipating  // 消散
        }

        private HandPhase Phase {
            get => (HandPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private int Direction => (int)Projectile.ai[1]; // -1 左手, 1 右手
        private ref float PhaseTimer => ref Projectile.localAI[0];
        private ref float ClapFlash => ref Projectile.localAI[1]; // 拍击冲击扭曲衰减标量
        private Vector2 startPos;
        private Vector2 clapTarget;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            PhaseTimer++;
            if (ClapFlash > 0f)
                ClapFlash *= 0.86f;

            switch (Phase) {
                case HandPhase.Appearing:
                    HandleAppearing();
                    break;
                case HandPhase.Charging:
                    HandleCharging();
                    break;
                case HandPhase.Clapping:
                    HandleClapping();
                    break;
                case HandPhase.Dissipating:
                    HandleDissipating();
                    break;
            }

            // 旋转朝向中心
            if (Phase != HandPhase.Dissipating) {
                Vector2 toCenter = (clapTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.rotation = toCenter.ToRotation() + (Direction > 0 ? 0 : MathHelper.Pi);
            }
        }

        private void HandleAppearing() {
            if (PhaseTimer == 1) {
                startPos = Projectile.Center;
                clapTarget = Projectile.Center + new Vector2(-Direction * 150, 100);
            }

            // 淡入
            Projectile.alpha = (int)MathHelper.Lerp(255, 0, PhaseTimer / 20f);
            Projectile.scale = MathHelper.Lerp(0f, 1f, PhaseTimer / 20f);

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
            }

            if (PhaseTimer >= 20) {
                Phase = HandPhase.Charging;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f }, Projectile.Center);
            }
        }

        private void HandleCharging() {
            // 蓄力震动
            float wobble = MathF.Sin(PhaseTimer * 0.8f) * 8f;
            Projectile.Center = startPos + new Vector2(Direction * wobble, 0);

            // 蓄力粒子
            if (Main.rand.NextBool(3)) {
                Vector2 offset = Main.rand.NextVector2Circular(50, 50);
                int dust = Dust.NewDust(Projectile.Center + offset, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = -offset.SafeNormalize(Vector2.Zero) * 4f;
                Main.dust[dust].noGravity = true;
            }

            if (PhaseTimer >= 30) {
                Phase = HandPhase.Clapping;
                PhaseTimer = 0;
            }
        }

        private void HandleClapping() {
            // 快速向中心合拢
            if (PhaseTimer < 10) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, clapTarget, 0.4f);
            }
            // 拍击瞬间
            else if (PhaseTimer == 10) {
                Projectile.Center = clapTarget;
                ClapFlash = 1f; // 触发拍击空间冲击扭曲 (PreDraw 内消费)

                // 只让右手生成弹幕和音效
                if (Direction > 0 && Main.myPlayer == Projectile.owner) {
                    // 环形射弹
                    int projectileCount = 16;
                    for (int i = 0; i < projectileCount; i++) {
                        float angle = MathHelper.TwoPi * i / projectileCount;
                        Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;

                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                            ModContent.ProjectileType<CorpsesesBookWave>(), Projectile.damage,
                            Projectile.knockBack, Projectile.owner);
                    }

                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.2f }, Projectile.Center);
                }

                // 冲击波粒子
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(15, 15);
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.PurpleTorch, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
            // 停顿
            else if (PhaseTimer < 25) {
                Projectile.Center = clapTarget;
            }
            else {
                Phase = HandPhase.Dissipating;
                PhaseTimer = 0;
            }
        }

        private void HandleDissipating() {
            // 淡出
            Projectile.alpha = (int)MathHelper.Lerp(0, 255, PhaseTimer / 20f);
            Projectile.scale = MathHelper.Lerp(1f, 0f, PhaseTimer / 20f);
            Projectile.rotation += 0.3f * Direction;

            if (PhaseTimer >= 20) {
                Projectile.Kill();
            }
        }

        public override bool? CanDamage() {
            // 只在拍击阶段造成伤害
            return Phase == HandPhase.Clapping && PhaseTimer >= 10 && PhaseTimer < 15;
        }

        // 拍击空间冲击扭曲 (GenericWarp · void, 走全屏名额仲裁, 仅拍击瞬间短促)
        private static void ApplyClapWarp(Vector2 worldCenter, float intensity) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            Vector2 uv = (worldCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.18f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWarpScale"]?.SetValue(1.4f);
            fx.Parameters["uChroma"]?.SetValue(0.5f);
            fx.Parameters["uRadialPull"]?.SetValue(-0.6f); // 向外推 (拍击冲击波)
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 0.3f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = texture.Size() / 2f;
            SpriteEffects effects = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color bone = new Color(208, 232, 205);
            Color edge = TelegraphColors.GhostGreen;
            float vis = 1f - Projectile.alpha / 255f;

            // 拍击瞬间: 空间冲击扭曲
            if (ClapFlash > 0.06f)
                ApplyClapWarp(Projectile.Center, ClapFlash * 0.5f);

            // 出现 / 消散: 幽手虚实切换 (DissolveBurn 灼烧边缘)
            if (Phase == HandPhase.Appearing || Phase == HandPhase.Dissipating) {
                WeaponVFX.ApplyDissolveBurn(texture, Projectile.Center, null, bone,
                    Projectile.rotation, origin,
                    scale: Projectile.scale, threshold: 1f - vis,
                    intensity: MathHelper.Clamp(vis, 0.05f, 1f), edgeColor: edge,
                    edgeWidth: 0.12f, noiseScale: 2.2f, effects: effects);
                return false;
            }

            // 实体手掌 + 鬼绿光晕
            Color glow = edge; glow.A = 0;
            for (int i = 0; i < 3; i++) {
                Vector2 offset = new Vector2(MathF.Cos(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f),
                                            MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f)) * 4f;
                Main.EntitySpriteDraw(texture, Projectile.Center + offset - Main.screenPosition, null,
                    glow * 0.4f, Projectile.rotation, origin, Projectile.scale * 1.1f, effects);
            }
            Color body = Color.Lerp(bone, edge, 0.22f); body.A = 255;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                body, Projectile.rotation, origin, Projectile.scale, effects);

            return false;
        }
    }

    /// <summary>
    /// 法书冲击波弹幕
    /// </summary>
    public class CorpsesesBookWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.15f, 0.6f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch, 0, 0, 100, default, 1.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(4, 4);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D texture = ModContent.Request<Texture2D>("Terraria/Images/Projectile_" + ProjectileID.ShadowFlame).Value;
            Vector2 origin = texture.Size() / 2f;

            Color bone = new Color(210, 235, 205);
            Color edge = new Color(40, 110, 70);

            // 双层鬼绿拖尾 (外宽暗绿 + 内窄骨白)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
                outerColor: edge with { A = 130 }, innerColor: bone with { A = 190 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            // 主体 (骨白核 + 鬼绿光晕)
            Color glow = TelegraphColors.GhostGreen; glow.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow * 0.5f,
                Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, bone,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }
}


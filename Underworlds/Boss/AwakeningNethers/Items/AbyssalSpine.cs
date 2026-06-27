using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers.Items
{
    /// <summary>
    /// 冥渊龙脊 - 觉醒幽冥龙掉落的战士武器
    /// 由幽冥龙的脊椎骨锻造而成的巨型大刀
    /// 特效：挥砍时释放龙脊斩波，命中敌人时有几率撕裂空间
    /// </summary>
    public class AbyssalSpine : ModItem
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/AbyssalSpine";

        private int comboCounter = 0; // 连击计数器

        public override void SetDefaults() {
            Item.damage = 8580;
            Item.DamageType = DamageClass.Melee;
            Item.width = 90;
            Item.height = 90;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AbyssalSpineSlash>();
            Item.shootSpeed = 16f;
            Item.scale = 1.3f;
            Item.crit = 15;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥砍时产生幽冥粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch;
                var d = Dust.NewDustDirect(hitbox.TopLeft(), hitbox.Width, hitbox.Height, dustType);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = player.itemRotation.ToRotationVector2() * 3f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            comboCounter++;

            // 基础斩波
            Vector2 shootVel = velocity.SafeNormalize(Vector2.UnitX) * Item.shootSpeed;
            Projectile.NewProjectile(source, player.Center, shootVel, type, damage, knockback, player.whoAmI,
                ai0: comboCounter % 3); // ai0控制斩波类型

            // 每第三击发射额外的次元裂斩
            if (comboCounter % 3 == 0) {
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 sideVel = shootVel.RotatedBy(MathHelper.ToRadians(20 * i));
                    Projectile.NewProjectile(source, player.Center, sideVel * 0.8f,
                        ModContent.ProjectileType<AbyssalRiftSlash>(), (int)(damage * 0.7f), knockback * 0.5f, player.whoAmI);
                }

                // 第三击特效
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.3f }, player.Center);
                AwakeningNetherHelper.CreateSoulBurst(player.Center, 60f, 2, 10);
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时有几率触发次元撕裂
            if (Main.rand.NextBool(5)) {
                AwakeningNetherHelper.CreateDimensionTear(player.Center, target.Center, 0.6f);

                // 对周围敌人造成溅射伤害
                foreach (var npc in Main.npc) {
                    if (npc.active && !npc.friendly && npc.whoAmI != target.whoAmI &&
                        Vector2.Distance(npc.Center, target.Center) < 150f) {
                        npc.SimpleStrikeNPC((int)(damageDone * 0.3f), hit.HitDirection, hit.Crit, hit.Knockback);
                    }
                }
            }

            // 命中特效
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(8, 8);
            }

            // 冥渊命中演出 (幽冥紫径向辉光 + 冲击环) + 轻度屏震
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1.1f, owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, 3f);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AbyssalLore", "「由觉醒幽冥龙的脊椎锻造，斩裂空间的巨刃」"));
            tooltips.Add(new TooltipLine(Mod, "AbyssalEffect", "每第三击释放三道次元裂斩"));
            tooltips.Add(new TooltipLine(Mod, "AbyssalEffect2", "命中敌人有几率触发次元撕裂，造成范围伤害"));
        }
    }

    /// <summary>
    /// 冥渊龙脊斩波 - 主要斩击弹幕
    /// </summary>
    public class AbyssalSpineSlash : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/AbyssalSpineSlash";

        private float pulsePhase = 0f;
        private int SlashType => (int)Projectile.ai[0]; // 0,1,2三种斩波类型

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            pulsePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 根据斩波类型调整行为
            switch (SlashType) {
                case 0: // 直线斩波
                    if (Projectile.velocity.Length() < 20f)
                        Projectile.velocity *= 1.02f;
                    break;
                case 1: // 上扬斩波
                    Projectile.velocity.Y -= 0.2f;
                    break;
                case 2: // 下劈斩波
                    Projectile.velocity.Y += 0.15f;
                    Projectile.velocity *= 1.01f;
                    break;
            }

            // 粒子拖尾
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 10),
                    DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            // 能量粒子
            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.12f;
            Color beamCore = SlashType == 2 ? AwakeningNetherHelper.NetherCyan : AwakeningNetherHelper.AwakeningPurple;

            // 双层斩波拖尾 (外宽暗冥紫 + 内窄亮芯)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 26f * pulse,
                outerColor: AwakeningNetherHelper.VoidDarkPurple with { A = 150 },
                innerColor: beamCore with { A = 200 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            // 龙脊斩波光束 (BeamGrad): 沿斩波轴贯穿
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 beamStart = Projectile.Center - dir * 70f * pulse;
            Vector2 beamEnd = Projectile.Center + dir * 70f * pulse;
            ACMShaders.DrawBeam(beamStart, beamEnd, 22f * pulse,
                beamCore with { A = 220 }, AwakeningNetherHelper.VoidDarkPurple with { A = 120 },
                0.9f, 1.8f, 2.2f, 2.4f);

            // 核心刃体 (加性辉光 + 实体)
            Color glow = beamCore; glow.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glow * 0.45f,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.3f / 3f, SpriteEffects.None, 0);
            Color body = beamCore; body.A = 200;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, body,
                Projectile.rotation, origin, Projectile.scale * pulse / 3f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中特效
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.85f, owner: Projectile.owner);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);

            for (int i = 0; i < 15; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 冥渊裂斩 - 第三击的次元裂隙斩波
    /// </summary>
    public class AbyssalRiftSlash : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/AbyssalSpineSlash";

        private float pulsePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 25;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.alpha = 50;
        }

        public override void AI() {
            pulsePhase += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 加速
            Projectile.velocity *= 1.03f;

            // 次元裂隙粒子
            if (Main.rand.NextBool(2)) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                float offset = MathF.Sin(pulsePhase * 2f) * 15f;
                var d = Dust.NewDustPerfect(Projectile.Center + perpendicular * offset, DustID.PurpleCrystalShard);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.NetherCyan.ToVector3() * 0.4f);
        }

        // 次元裂斩的局部空间撕裂扭曲 (GenericWarp · rift, 走全屏名额仲裁, 仅出生瞬间短促触发)
        private static void ApplyRiftWarp(Vector2 worldCenter, float intensity) {
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
            fx.Parameters["uRadius"]?.SetValue(0.15f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWarpScale"]?.SetValue(1.3f);
            fx.Parameters["uChroma"]?.SetValue(0.65f);
            fx.Parameters["uRadialPull"]?.SetValue(0.8f);
            fx.Parameters["uMode"]?.SetValue(3f); // rift
            fx.Parameters["uTint"]?.SetValue(new Vector4(AwakeningNetherHelper.AwakeningPurple.ToVector3(), 0.4f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 出生瞬间撕裂空间 (GenericWarp), 随存活快速衰减
            float bornFade = MathHelper.Clamp((Projectile.timeLeft - 33f) / 12f, 0f, 1f);
            if (bornFade > 0.02f)
                ApplyRiftWarp(Projectile.Center, bornFade * 0.55f);

            // 锯齿裂隙双层拖尾 (紫→青冥流)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 18f,
                outerColor: AwakeningNetherHelper.VoidDarkPurple with { A = 140 },
                innerColor: AwakeningNetherHelper.NetherCyan with { A = 180 },
                uvScroll: Main.GlobalTimeWrappedHourly * 2f);

            // 裂隙光束 (BeamGrad, 青冥芯)
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 55f, Projectile.Center + dir * 55f, 14f,
                AwakeningNetherHelper.NetherCyan with { A = 220 },
                AwakeningNetherHelper.VoidDarkPurple with { A = 120 }, 0.95f, 2.2f, 2.4f, 2.6f);

            // 主体 - 带有闪烁效果
            float flash = MathF.Sin(pulsePhase * 3f) * 0.3f + 0.7f;
            Color mainColor = Color.Lerp(AwakeningNetherHelper.AwakeningPurple,
                AwakeningNetherHelper.NetherCyan, 0.5f) * flash;
            mainColor.A = 150;

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, Projectile.scale / 3f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 次元裂隙命中效果
            AwakeningNetherHelper.CreateDimensionTear(Projectile.Center, target.Center, 0.4f);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.9f, owner: Projectile.owner);

            target.AddBuff(BuffID.ShadowFlame, 180);
        }

        public override void OnKill(int timeLeft) {
            // 消散时的裂隙爆发
            AwakeningNetherHelper.CreateSoulBurst(Projectile.Center, 40f, 1, 8);
        }
    }
}

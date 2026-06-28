using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences.Items
{
    /// <summary>
    /// 勾魂法杖 - 白无常掉落的魔法法杖
    /// 在鼠标位置召唤旋转的幽灵法阵，持续伤害范围内敌人并吸取生命
    /// </summary>
    public class DemonSoulStaff : ModItem
    {
        public override string Texture => BAWHelper.Path + "Items/DemonSoulStaff";

        public override void SetDefaults() {
            Item.damage = 278;
            Item.DamageType = DamageClass.Magic;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.LightPurple;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DemonSoulCircle>();
            Item.shootSpeed = 0f;
            Item.mana = 18;
            Item.channel = false;
            Item.staff[Item.type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 在鼠标位置召唤法阵
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // 施法特效
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f }, targetPos);
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                Vector2 dustVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                var d = Dust.NewDustPerfect(targetPos + dustVel * 10, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -dustVel;
            }
            // 召阵演出 (更新阶段禁止直接绘制 — IRON RULE 1)
            ACMWeaponBurst.Spawn(player.GetSource_ItemUse(Item), targetPos,
                ACMWeaponBurst.AbyssPurple, scale: 1f, owner: player.whoAmI);

            return false;
        }

        public override void AddRecipes() {
            // 可以添加合成配方
        }
    }

    /// <summary>
    /// 勾魂法阵 - 持续伤害范围内敌人的魔法阵
    /// </summary>
    public class DemonSoulCircle : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float circleAlpha = 0f;
        private float circleRadius = 80f;
        private int damageTimer = 0;
        private int healAccumulator = 0;

        public override void SetDefaults() {
            Projectile.width = 160;
            Projectile.height = 160;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            // 淡入效果
            if (Projectile.timeLeft > 160) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 1f, 0.1f);
                circleRadius = MathHelper.Lerp(circleRadius, 100f, 0.05f);
            }
            // 淡出效果
            else if (Projectile.timeLeft < 30) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 0f, 0.05f);
                circleRadius = MathHelper.Lerp(circleRadius, 150f, 0.03f);
            }

            pulsePhase += 0.12f;
            damageTimer++;

            // 检测范围内敌人并造成伤害
            if (damageTimer >= 15) {
                damageTimer = 0;
                foreach (var npc in Main.npc) {
                    if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < circleRadius) {
                            // 对敌人造成伤害
                            Player owner = Main.player[Projectile.owner];
                            npc.SimpleStrikeNPC(Projectile.damage / 3, 0, false, 0, DamageClass.Magic);

                            // 累积治疗
                            healAccumulator += 2;

                            // 吸取特效
                            SpawnDrainEffect(npc.Center);
                        }
                    }
                }

                // 每累积一定量治疗玩家
                if (healAccumulator >= 5) {
                    Player owner = Main.player[Projectile.owner];
                    owner.Heal(healAccumulator);
                    healAccumulator = 0;
                }
            }

            // 法阵边缘粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circleRadius;
                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f * circleAlpha;
                d.velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2), MathF.Sin(angle + MathHelper.PiOver2)) * 2f;
                d.alpha = 100;
            }

            // 中心粒子漩涡
            if (Main.rand.NextBool(3)) {
                float spiralAngle = pulsePhase * 2f + Main.rand.NextFloat(MathHelper.TwoPi);
                float spiralRadius = Main.rand.NextFloat(circleRadius * 0.3f, circleRadius * 0.8f);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralRadius;
                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.6f * circleAlpha;
                d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
                d.alpha = 80;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(150, 130, 220).ToVector3() * 0.5f * circleAlpha);
        }

        private void SpawnDrainEffect(Vector2 targetPos) {
            // 从敌人位置到法阵中心的吸取粒子
            Vector2 direction = (Projectile.Center - targetPos).SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 5; i++) {
                float t = i / 5f;
                Vector2 pos = Vector2.Lerp(targetPos, Projectile.Center, t);
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(5, 5), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.7f * (1f - t);
                d.velocity = direction * 6f;
                d.alpha = 100;
            }
        }

        public override bool? CanHitNPC(NPC target) {
            // 使用自定义伤害逻辑，禁用默认碰撞伤害
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            // 1) 旋转幽灵符阵 (ArenaRunic 法阵, 替代 BAWDust 叠环占位)
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                SpriteBatch sb = Main.spriteBatch;
                ACMShaders.WorldDecalParams(Projectile.Center, circleRadius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(circleAlpha * 0.9f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Color(190, 155, 255).ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(new Color(120, 90, 215).ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(13f);
                fx.Parameters["uMode"]?.SetValue(0f);  // 法阵
                fx.Parameters["uShape"]?.SetValue(0f); // 圆
                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 2) 吸血能量束: 范围内敌人 → 法阵中心 (BeamGrad, 上限 4 条控开销)
            int beams = 0;
            foreach (var npc in Main.ActiveNPCs) {
                if (beams >= 4) break;
                if (npc.friendly || !npc.CanBeChasedBy()) continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) < circleRadius) {
                    ACMShaders.DrawBeam(npc.Center, Projectile.Center, 4f,
                        new Color(215, 185, 255), new Color(125, 75, 200), circleAlpha * 0.7f,
                        flowSpeed: 3.2f, flowScale: 2.6f);
                    beams++;
                }
            }

            // 3) 法阵核心: 径向泛光 + 柔光
            float corePulse = 1f + MathF.Sin(pulsePhase * 1.5f) * 0.2f;
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.06f, circleAlpha * 0.6f, new Color(180, 150, 255), 8f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.3f * corePulse * circleAlpha + 0.2f, new Color(170, 130, 250));
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);

            // 消散特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30;
                Vector2 dustVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                var d = Dust.NewDustPerfect(Projectile.Center + dustVel * 5, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = dustVel;
            }
        }
    }
}

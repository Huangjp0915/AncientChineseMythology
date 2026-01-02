using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers.Items
{
    /// <summary>
    /// 蚀魂权杖 - 觉醒幽冥龙掉落的法师武器
    /// 由幽冥龙的灵魂能量凝聚而成的权杖
    /// 特效：在目标位置召唤蚀魂法阵，持续侵蚀敌人灵魂并吸取生命
    /// </summary>
    public class SoulErosionScepter : ModItem
    {
        public override string Texture => AwakeningNetherHelper.Path + "Items/SoulErosionScepter";

        public override void SetDefaults() {
            Item.damage = 8380;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulErosionCircle>();
            Item.shootSpeed = 0f;
            Item.mana = 25;
            Item.staff[Item.type] = true;
            Item.crit = 10;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 targetPos = Main.MouseWorld;

            // 限制同时存在的法阵数量
            int existingCircles = player.ownedProjectileCounts[type];
            if (existingCircles >= 3) {
                // 销毁最老的法阵
                foreach (var proj in Main.projectile) {
                    if (proj.active && proj.owner == player.whoAmI && proj.type == type) {
                        proj.Kill();
                        break;
                    }
                }
            }

            // 召唤蚀魂法阵
            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // 施法特效
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.1f, Volume = 1.1f }, targetPos);
            AwakeningNetherHelper.CreateSoulBurst(targetPos, 80f, 2, 16);

            // 从玩家到目标位置的能量连接
            AwakeningNetherHelper.CreateDimensionTear(player.Center, targetPos, 0.5f);

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "SoulLore", "「蕴含幽冥龙灵魂力量的权杖，能侵蚀一切生灵的灵魂」"));
            tooltips.Add(new TooltipLine(Mod, "SoulEffect", "在目标位置召唤蚀魂法阵（最多3个）"));
            tooltips.Add(new TooltipLine(Mod, "SoulEffect2", "法阵持续侵蚀敌人灵魂，并为你恢复生命"));
            tooltips.Add(new TooltipLine(Mod, "SoulEffect3", "法阵之间会产生灵魂连锁，增强伤害"));
        }
    }

    /// <summary>
    /// 蚀魂法阵 - 区域控制弹幕
    /// </summary>
    public class SoulErosionCircle : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float pulsePhase = 0f;
        private float circleScale = 0f;
        private float runeRotation = 0f;
        private int damageTimer = 0;
        private const float MaxScale = 1.5f;
        private const int DamageInterval = 15;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300; // 5秒持续时间
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = DamageInterval;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            runeRotation += 0.03f;
            damageTimer++;

            // 法阵展开动画
            if (Projectile.timeLeft > 280) {
                circleScale = MathHelper.Lerp(circleScale, MaxScale, 0.1f);
            }
            // 法阵收缩消失
            else if (Projectile.timeLeft < 30) {
                circleScale = MathHelper.Lerp(circleScale, 0f, 0.1f);
            }

            // 调整碰撞范围
            Projectile.width = Projectile.height = (int)(200 * circleScale);
            Projectile.Center = Projectile.Center; // 重新定位中心

            // 周期性伤害和效果
            if (damageTimer >= DamageInterval) {
                damageTimer = 0;
                DealAreaDamage();
            }

            // 与其他法阵形成连锁
            CreateChainLinks();

            // 法阵粒子效果
            CreateCircleParticles();

            // 发光
            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * circleScale);
        }

        private void DealAreaDamage() {
            Player owner = Main.player[Projectile.owner];
            float damageRadius = 100f * circleScale;
            int healAmount = 0;

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage &&
                    Vector2.Distance(npc.Center, Projectile.Center) < damageRadius) {
                    // 造成伤害
                    npc.SimpleStrikeNPC(Projectile.damage, 0, false, 0);

                    // 蚀魂效果
                    npc.AddBuff(BuffID.ShadowFlame, 60);

                    // 累计治疗量
                    healAmount += 5;

                    // 灵魂被吸取的视觉效果
                    for (int i = 0; i < 3; i++) {
                        Vector2 soulPos = npc.Center + Main.rand.NextVector2Circular(20, 20);
                        var d = Dust.NewDustPerfect(soulPos, DustID.SpectreStaff);
                        d.noGravity = true;
                        d.scale = 1.2f;
                        d.velocity = (Projectile.Center - soulPos).SafeNormalize(Vector2.Zero) * 8f;
                    }
                }
            }

            // 治疗玩家
            if (healAmount > 0 && owner.statLife < owner.statLifeMax2) {
                owner.statLife += Math.Min(healAmount, 20); // 最多回复20
                owner.HealEffect(Math.Min(healAmount, 20));
            }
        }

        private void CreateChainLinks() {
            // 寻找其他蚀魂法阵并形成连锁
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.owner == Projectile.owner &&
                    proj.type == Projectile.type && proj.whoAmI != Projectile.whoAmI) {
                    float dist = Vector2.Distance(Projectile.Center, proj.Center);

                    // 在连线范围内
                    if (dist < 1500f && dist > 50f) {
                        // 连锁伤害敌人
                        if (damageTimer == 0) {
                            DealChainDamage(Projectile.Center, proj.Center);
                        }

                        // 连锁粒子效果
                        if (Main.rand.NextBool(5)) {
                            float t = Main.rand.NextFloat();
                            Vector2 particlePos = Vector2.Lerp(Projectile.Center, proj.Center, t);
                            var d = Dust.NewDustPerfect(particlePos + Main.rand.NextVector2Circular(10, 10), DustID.PurpleTorch);
                            d.noGravity = true;
                            d.scale = 0.8f;
                            d.velocity = Main.rand.NextVector2Circular(1, 1);
                        }
                    }
                }
            }
        }

        private void DealChainDamage(Vector2 start, Vector2 end) {
            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage) {
                    // 检查敌人是否在连线上
                    Vector2 toNpc = npc.Center - start;
                    float projLength = Vector2.Dot(toNpc, direction);

                    if (projLength > 0 && projLength < distance) {
                        Vector2 closestPoint = start + direction * projLength;
                        float perpDist = Vector2.Distance(npc.Center, closestPoint);

                        if (perpDist < 50f) {
                            // 连锁伤害
                            npc.SimpleStrikeNPC((int)(Projectile.damage * 0.5f), 0, false, 0);

                            // 连锁命中特效
                            for (int i = 0; i < 5; i++) {
                                var d = Dust.NewDustPerfect(npc.Center, DustID.Shadowflame);
                                d.noGravity = true;
                                d.scale = 1.3f;
                                d.velocity = Main.rand.NextVector2Circular(4, 4);
                            }
                        }
                    }
                }
            }
        }

        private void CreateCircleParticles() {
            float effectiveRadius = 80f * circleScale;

            // 外圈旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * effectiveRadius;

                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.2f * circleScale;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f;
            }

            // 内部漩涡粒子
            if (Main.rand.NextBool(3)) {
                float angle = runeRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(0.3f, 0.8f) * effectiveRadius;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                var d = Dust.NewDustPerfect(pos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
            }

            // 符文闪烁粒子
            if (Main.rand.NextBool(8)) {
                float runeAngle = runeRotation + MathHelper.TwoPi * Main.rand.Next(8) / 8f;
                Vector2 runePos = Projectile.Center + new Vector2(MathF.Cos(runeAngle), MathF.Sin(runeAngle)) * effectiveRadius * 0.6f;

                var d = Dust.NewDustPerfect(runePos, DustID.PurpleCrystalShard);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = Vector2.Zero;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制法阵
            DrawMagicCircle(sb);

            // 绘制与其他法阵的连线
            DrawChainConnections(sb);

            // 绘制中心核心
            DrawCore(sb);

            return false;
        }

        private void DrawMagicCircle(SpriteBatch sb) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            float effectiveRadius = 80f * circleScale;

            // 多层法阵环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = effectiveRadius * (0.6f + ring * 0.2f);
                float ringRotation = runeRotation * (ring % 2 == 0 ? 1 : -1);
                int segments = 12 - ring * 2;
                float ringAlpha = (0.6f - ring * 0.15f) * circleScale;

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    float pulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                    Color runeColor = Color.Lerp(AwakeningNetherHelper.VoidDarkPurple,
                        AwakeningNetherHelper.AwakeningPurple, pulse) * ringAlpha;
                    runeColor.A = 0;

                    float runeScale = (0.8f + pulse * 0.3f) * circleScale;
                    sb.Draw(tex, pos - Main.screenPosition, null, runeColor,
                        angle + MathHelper.PiOver4, origin, runeScale, SpriteEffects.None, 0);
                }

                // 环之间的连线
                for (int i = 0; i < segments / 2; i++) {
                    float angle1 = ringRotation + MathHelper.TwoPi * i * 2 / segments;
                    float angle2 = ringRotation + MathHelper.TwoPi * ((i * 2 + segments / 3) % segments) / segments;

                    Vector2 pos1 = Projectile.Center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * ringRadius;
                    Vector2 pos2 = Projectile.Center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * ringRadius;

                    Color lineColor = AwakeningNetherHelper.NetherCyan * ringAlpha * 0.3f;
                    AwakeningNetherHelper.DrawEnergyBeam(sb, pos1, pos2, lineColor, 3f * circleScale, pulsePhase);
                }
            }

            // 外圈边界
            int borderSegments = 36;
            for (int i = 0; i < borderSegments; i++) {
                float angle = MathHelper.TwoPi * i / borderSegments;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * effectiveRadius;

                float pulse = MathF.Sin(pulsePhase * 2f + angle * 4f) * 0.3f + 0.7f;
                Color borderColor = AwakeningNetherHelper.AwakeningPurple * pulse * circleScale * 0.5f;
                borderColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, borderColor,
                    angle, origin, 0.6f * circleScale, SpriteEffects.None, 0);
            }
        }

        private void DrawChainConnections(SpriteBatch sb) {
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.owner == Projectile.owner &&
                    proj.type == Projectile.type && proj.whoAmI != Projectile.whoAmI) {
                    float dist = Vector2.Distance(Projectile.Center, proj.Center);

                    if (dist < 1500f && dist > 50f) {
                        // 只绘制一次（索引小的绘制）
                        if (Projectile.whoAmI < proj.whoAmI) {
                            Color chainColor = Color.Lerp(AwakeningNetherHelper.AwakeningPurple,
                                AwakeningNetherHelper.SoulPink, MathF.Sin(pulsePhase) * 0.5f + 0.5f);
                            chainColor *= circleScale * 0.6f;

                            AwakeningNetherHelper.DrawEnergyBeam(sb, Projectile.Center, proj.Center,
                                chainColor, 8f * circleScale, pulsePhase, true);
                        }
                    }
                }
            }
        }

        private void DrawCore(SpriteBatch sb) {
            // 中心虚空核心
            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center,
                AwakeningNetherHelper.AwakeningPurple,
                AwakeningNetherHelper.NetherCyan,
                circleScale * 1.2f, pulsePhase);

            // 核心周围的灵魂环绕
            if (circleScale > 0.5f) {
                AwakeningNetherHelper.DrawSoulOrbit(sb, Projectile.Center, 30f * circleScale, 4,
                    pulsePhase * 1.5f, pulsePhase,
                    [AwakeningNetherHelper.AwakeningPurple, AwakeningNetherHelper.NetherCyan,
                     AwakeningNetherHelper.SoulPink, AwakeningNetherHelper.VoidDarkPurple]);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);

            // 消散特效
            AwakeningNetherHelper.CreateVoidVortex(Projectile.Center, 100f * circleScale, 0.8f, 30);
            AwakeningNetherHelper.CreateSoulBurst(Projectile.Center, 80f * circleScale, 3, 16);

            // 最后一击伤害
            float damageRadius = 120f * circleScale;
            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage &&
                    Vector2.Distance(npc.Center, Projectile.Center) < damageRadius) {
                    npc.SimpleStrikeNPC((int)(Projectile.damage * 0.5f), 0, false, 0);
                }
            }
        }

        // 禁用默认碰撞伤害，使用自定义的区域伤害
        public override bool? CanHitNPC(NPC target) => false;
    }
}

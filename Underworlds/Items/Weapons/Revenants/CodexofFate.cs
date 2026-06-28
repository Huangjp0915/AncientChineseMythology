using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
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
    /// 表现重做: 双层带状拖尾 + 程序化 <see cref="ACMShaders.ArenaRunic"/> 符文环 (替代静态 BlankStar 旋转,
    /// 每帧仅一枚符文承担全屏法阵绘制以控开销, 其余退化为廉价星光); 暴击链式电弧走专属
    /// <see cref="FateJudgmentField"/> 用 <see cref="ACMShaders.DrawBeam"/> 折线束连击。
    /// </summary>
    public class FateRuneProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/CodexofFate";

        private ref float RotationTimer => ref Projectile.ai[0];

        // 每帧只允许一枚符文绘制全屏 ArenaRunic 法阵环 (开销护栏: 不占用全屏后处理名额, 仅本类内部节流)
        private static ulong _lastRuneRingFrame;

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

                //暴击: 命运链电演出 (ArenaRunic 判词环 + DrawBeam 折线电链, 纯视觉一次性弹幕)
                FateJudgmentField.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 2.5f);
            }

            //命中冲击演出 (径向辉光 + 冲击环), 走 ACMWeaponBurst 暗冥紫主题
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.25f : 0.85f, owner: Projectile.owner);

            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //双层带状拖尾 (外宽暗冥紫 + 内窄亮紫芯)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(90, 40, 150, 150), innerColor: new Color(190, 120, 255, 200),
                uvScroll: RotationTimer * 0.02f);

            //程序化符文环 (ArenaRunic 法阵): 每帧仅一枚承担全屏绘制, 其余退化为廉价星光
            bool drawRuneRing = false;
            if (_lastRuneRingFrame != Main.GameUpdateCount) {
                _lastRuneRingFrame = Main.GameUpdateCount;
                drawRuneRing = true;
            }

            if (drawRuneRing) {
                Effect fx = ACMShaders.ArenaRunic;
                if (fx != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    ACMShaders.WorldDecalParams(Projectile.Center, 34f, out Vector2 uv, out float rFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uRadius"]?.SetValue(rFrac);
                    fx.Parameters["uIntensity"]?.SetValue(0.7f);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uColorPrimary"]?.SetValue(new Color(190, 130, 255).ToVector4());
                    fx.Parameters["uColorSecondary"]?.SetValue(new Color(80, 35, 150).ToVector4());
                    fx.Parameters["uRuneFreq"]?.SetValue(12f);
                    fx.Parameters["uMode"]?.SetValue(0f);
                    fx.Parameters["uShape"]?.SetValue(0f);

                    sb.End();
                    ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }
            else {
                //廉价星光环 (本帧法阵名额被其它符文占用时的退化表现)
                Texture2D blankStar = ACMAsset.BlankStar;
                if (blankStar != null) {
                    Vector2 starOrigin = blankStar.Size() / 2f;
                    Color starColor = new Color(200, 150, 255) * 0.5f;
                    starColor.A = 0;
                    float starScale = 0.2f + MathF.Sin(RotationTimer * 0.3f) * 0.05f;
                    Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, RotationTimer * 0.15f, starOrigin, starScale, SpriteEffects.None, 0);
                }
            }

            //符文核心光球 (SoftGlow 呼吸脉动)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = new Color(180, 100, 255) * 0.6f;
                mainGlow.A = 0;
                float pulse = 0.55f + MathF.Sin(RotationTimer * 0.2f) * 0.08f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
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

    /// <summary>
    /// 命运链电演出弹幕 (纯视觉, damage=0): 暴击瞬间在命中点展开 <see cref="ACMShaders.ArenaRunic"/> 判词法阵环,
    /// 并以 <see cref="ACMShaders.DrawBeam"/> 在命中点 → 邻近敌群之间拉出折线电链 (polyline)。
    /// 绘制只在 PreDraw, 命中阶段仅 <see cref="Spawn"/> 触发 (更新阶段安全, 仅 owner 客户端)。
    /// </summary>
    public class FateJudgmentField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;
        private const float RingRadius = 200f;
        private const float ChainRange = 260f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<FateJudgmentField>(), 0, 0f, owner);
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

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.25f, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;             // 0→1
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);

            SpriteBatch sb = Main.spriteBatch;

            //—— ArenaRunic 判词法阵环 (扩张 + 呼吸) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                float radius = RingRadius * (0.5f + life * 0.5f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fade * 0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Color(190, 130, 255).ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(new Color(80, 35, 150).ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(13f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            //—— DrawBeam 折线电链: 命中点 → 最近邻敌依次跳跃 (polyline, 最多 5 跳) ——
            var nodes = new List<Vector2> { Projectile.Center };
            var used = new HashSet<int>();
            Vector2 cursor = Projectile.Center;
            for (int hop = 0; hop < 5; hop++) {
                int best = -1;
                float bestDist = ChainRange;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || used.Contains(i))
                        continue;
                    float d = Vector2.Distance(cursor, npc.Center);
                    if (d < bestDist) {
                        bestDist = d;
                        best = i;
                    }
                }
                if (best < 0)
                    break;
                used.Add(best);
                cursor = Main.npc[best].Center;
                nodes.Add(cursor);
            }

            for (int i = 0; i < nodes.Count - 1; i++) {
                ACMShaders.DrawBeam(nodes[i], nodes[i + 1], 6f * fade,
                    new Color(210, 170, 255), new Color(110, 55, 215), fade * 0.9f,
                    flowSpeed: 3.4f, flowScale: 3.2f, coreSharp: 2.6f);
            }

            //—— 中心核辉光 (峰值期申请全屏名额, 退化为柔光) ——
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.07f, fade * 0.65f, new Color(170, 120, 250), 8f);

            return false;
        }
    }
}

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
    /// 生死因果万魔录 - CodexofFate的终极升级版
    /// 记载万魔因果、掌控生死轮回的终极秘典
    /// 特殊机制：发射5枚追踪魔符，暴击时全范围链式雷击，击杀后召唤魔灵继续攻击
    /// </summary>
    public class CodexofMyriadDemons : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 220;
            Item.crit = 18;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.width = 44;
            Item.height = 44;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<MyriadDemonRune>();
            Item.shootSpeed = 20f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 发射5枚万魔符文
            for (int i = 0; i < 5; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                perturbedSpeed *= Main.rand.NextFloat(0.85f, 1.15f);
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            // 施法粒子效果
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust page = Dust.NewDustPerfect(position, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.2f, 2f));
                page.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust arc = Dust.NewDustPerfect(position, DustID.Electric, vel, 60, default, 1.2f);
                arc.noGravity = true;
            }
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 30f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CodexofFate>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class MyriadDemonRune : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/CodexofMyriadDemons";
        private ref float RotationTimer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            RotationTimer++;
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 1.2f);

            // 更强的追踪，更远的距离
            if (RotationTimer > 8f) {
                NPC target = FindClosestNPC(700f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.07f);
                }
            }

            for (int i = 0; i < 2; i++) {
                Dust rune = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(10, 10),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(1.2f, 2f)
                );
                rune.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust arc = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.Electric,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    60, default, 1.2f
                );
                arc.noGravity = true;
            }
            // 魔符旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = RotationTimer * 0.4f;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                Dust orbit = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Shadowflame,
                    -offset.X * 0.15f, -offset.Y * 0.15f,
                    100, default, 1f
                );
                orbit.noGravity = true;
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
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.Electrified, 300);

            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust bolt = Dust.NewDustPerfect(target.Center, DustID.Electric, vel, 60, default, Main.rand.NextFloat(1.5f, 2.5f));
                bolt.noGravity = true;
            }
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                burst.noGravity = true;
            }

            // 全范围链式雷击：暴击时连锁5个敌人
            if (hit.Crit) {
                int chainCount = 0;
                NPC currentTarget = target;
                for (int i = 0; i < Main.maxNPCs && chainCount < 5; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == currentTarget.whoAmI) continue;
                    float dist = Vector2.Distance(currentTarget.Center, nearby.Center);
                    if (dist < 400f) {
                        nearby.SimpleStrikeNPC(damageDone / 3, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.Electrified, 180);
                        // 链式闪电视觉效果
                        for (int j = 0; j < 12; j++) {
                            float t = j / 12f;
                            Vector2 pos = Vector2.Lerp(currentTarget.Center, nearby.Center, t);
                            pos += Main.rand.NextVector2Circular(6f, 6f);
                            Dust chain = Dust.NewDustPerfect(pos, DustID.Electric, Main.rand.NextVector2Circular(2f, 2f), 60, default, 1.5f);
                            chain.noGravity = true;
                        }
                        chainCount++;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.3f }, target.Center);
                // 升级演出: 万魔法阵 + 多目标电网 (ArenaRunic + BeamGrad), 仅本机生成
                MyriadDemonRuneField.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 4f);
            }

            // 命中冲击演出 (径向辉光 + 冲击环), 走 ACMWeaponBurst 暗冥紫主题
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.5f : 1f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2f);

            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.3f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 双层带状拖尾 (外宽暗冥紫 + 内窄亮紫芯), 取代旧逐帧叠贴图拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, 18f,
                new Color(90, 40, 170), new Color(200, 150, 255),
                uvScroll: RotationTimer * 0.02f);

            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcIndex = (int)(RotationTimer * 0.12f) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
                Color arcColor = new Color(200, 120, 255) * 0.5f;
                arcColor.A = 0;
                float arcScale = 0.18f + MathF.Sin(RotationTimer * 0.25f) * 0.03f;
                Main.EntitySpriteDraw(arcSheet, Projectile.Center - Main.screenPosition, sourceRect, arcColor, Projectile.rotation + MathHelper.PiOver2, arcOrigin, arcScale, SpriteEffects.None, 0);
            }

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = new Color(220, 120, 255) * 0.8f;
                mainGlow.A = 0;
                float pulse = 0.7f + MathF.Sin(RotationTimer * 0.25f) * 0.12f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(240, 180, 255) * 0.7f;
                starColor.A = 0;
                float starScale = 0.3f + MathF.Sin(RotationTimer * 0.35f) * 0.08f;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, RotationTimer * 0.2f, starOrigin, starScale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f }, Projectile.Center);
            for (int i = 0; i < 18; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                death.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust arc = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Electric,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    60, default, 1.2f
                );
                arc.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 万魔法阵演出弹幕 (纯视觉, damage=0): 暴击连锁瞬间在敌群中心展开 ArenaRunic 万魔法阵地纹,
    /// 并用 BeamGrad 向周围 NPC 拉出多目标电网。绘制只在 PreDraw, 命中阶段仅 <see cref="Spawn"/> 触发。
    /// </summary>
    public class MyriadDemonRuneField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 44;
        private const float FieldRadius = 360f;
        private const float WebRange = 420f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<MyriadDemonRuneField>(), 0, 0f, owner);
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
            Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;            // 0→1
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);
            Color primary = new Color(170, 110, 255);
            Color secondary = new Color(70, 30, 130);

            SpriteBatch sb = Main.spriteBatch;

            // —— ArenaRunic 万魔法阵地纹 (扩张 + 呼吸) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                float radius = FieldRadius * (0.45f + life * 0.55f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fade * 0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(14f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // —— BeamGrad 多目标电网: 中心 → 周围敌人 (最多 6 条) ——
            int web = 0;
            for (int i = 0; i < Main.maxNPCs && web < 6; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;
                if (Vector2.Distance(Projectile.Center, npc.Center) > WebRange)
                    continue;
                ACMShaders.DrawBeam(Projectile.Center, npc.Center, 7f * fade,
                    new Color(220, 180, 255), new Color(120, 60, 220), fade * 0.9f,
                    flowSpeed: 3.2f, flowScale: 3f, coreSharp: 2.6f);
                web++;
            }

            // —— 中心核辉光 (峰值期申请全屏名额, 退化为柔光) ——
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.08f, fade * 0.7f, new Color(160, 110, 245), 10f);

            return false;
        }
    }
}

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
    /// 黄泉寂灭冥罗杖 - StaveofNetherflow的终极升级版
    /// 凝聚黄泉寂灭之力、显化冥府罗网的法杖
    /// 特殊机制：同时发射3枚寂灭冥球，命中后产生冥府罗网束缚区域，持续造成伤害
    /// </summary>
    public class StaveofNetherEclipse : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1460;
            Item.crit = 14;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherEclipseOrb>();
            Item.shootSpeed = 14f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 staffTip = player.Center + velocity.SafeNormalize(Vector2.Zero) * 60f;
            // 同时发射3枚寂灭冥球
            for (int i = -1; i <= 1; i++) {
                Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(i * 10));
                Projectile.NewProjectile(source, staffTip, perturbedVel, type, damage, knockback, player.whoAmI);
            }
            // 施法粒子爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(40)) * Main.rand.NextFloat(3f, 8f);
                Dust cast = Dust.NewDustPerfect(staffTip, DustID.Wraith, vel, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                cast.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust eclipse = Dust.NewDustPerfect(staffTip, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(1.5f, 2.5f));
                eclipse.noGravity = true;
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StaveofNetherflow>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class NetherEclipseOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/StaveofNetherEclipse";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 200;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.15f;

            // 更强的追踪能力
            if (Timer > 12f) {
                NPC target = FindClosestNPC(600f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.05f);
                }
            }

            float pulse = 0.8f + MathF.Sin(Timer * 0.18f) * 0.2f;
            Lighting.AddLight(Projectile.Center, 0.4f * pulse, 1f * pulse, 1.2f * pulse);

            // 冥罗旋涡粒子
            for (int i = 0; i < 2; i++) {
                float angle = Timer * 0.35f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(10f, 22f);
                Dust vortex = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Wraith,
                    -offset.X * 0.15f, -offset.Y * 0.15f,
                    100, default, Main.rand.NextFloat(1.2f, 2f)
                );
                vortex.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f, 4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.25f, -Projectile.velocity.Y * 0.25f,
                    120, default, Main.rand.NextFloat(1f, 1.8f)
                );
                trail.noGravity = true;
            }
            // 寂灭微光
            if (Main.rand.NextBool(3)) {
                Dust glow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.PurpleTorch,
                    0f, -0.5f, 80, default, 1.5f
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
                if (dist < closestDist) { closestDist = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 360);
            target.AddBuff(BuffID.Slow, 300);
            target.AddBuff(BuffID.Frostburn2, 300);

            // 冥罗网爆发：大范围涡旋效果
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi / 30f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 12f);
                Dust vortex = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(2f, 3.2f));
                vortex.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                Dust ring = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(2f, 3f));
                ring.noGravity = true;
            }

            // 寂灭区域：对附近所有敌人施加减速和暗影焰
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 300f) {
                    nearby.AddBuff(BuffID.ShadowFlame, 180);
                    nearby.AddBuff(BuffID.Slow, 180);
                    nearby.SimpleStrikeNPC(damageDone / 4, hit.HitDirection, false, 0f, null, false, 0, true);
                }
            }

            // 升级演出: 冥府罗网束缚结界 (ArenaRunic 牢笼网) + 幽冥龙青命中爆, 仅本机生成
            NetherNetField.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 1.3f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 3f);

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 冥罗青蓝双层带状拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, 16f,
                new Color(30, 110, 150), new Color(110, 240, 255),
                uvScroll: Timer * 0.025f);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float pulse1 = 1f + MathF.Sin(Timer * 0.18f) * 0.15f;
                Color innerGlow = new Color(100, 250, 255) * 0.8f;
                innerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, innerGlow, 0f, glowOrigin, pulse1, SpriteEffects.None, 0);
                float pulse2 = 1.3f + MathF.Sin(Timer * 0.12f + 1f) * 0.2f;
                Color outerGlow = new Color(50, 140, 200) * 0.4f;
                outerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, outerGlow, 0f, glowOrigin, pulse2, SpriteEffects.None, 0);
            }

            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frame = (int)(Timer * 0.35f) % 16;
                int frameX = frame % 4;
                int frameY = frame / 4;
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Rectangle sourceRect = new Rectangle(frameX * frameW, frameY * frameH, frameW, frameH);
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);
                Color smokeColor = new Color(80, 180, 220) * 0.3f;
                smokeColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, smokeColor, Timer * 0.06f, smokeOrigin, 0.2f, SpriteEffects.None, 0);
            }

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(140, 230, 255) * 0.6f;
                starColor.A = 0;
                float starScale = 0.35f + MathF.Sin(Timer * 0.3f) * 0.1f;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.12f, starOrigin, starScale, SpriteEffects.None, 0);
            }

            // 寂灭冥球核辉光 (RadialBloom, 走全屏名额, 满则退化为柔光)
            float corePulse = 0.5f + MathF.Sin(Timer * 0.18f) * 0.12f;
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.05f, corePulse, new Color(110, 240, 255), 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                Dust death = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                death.noGravity = true;
            }
            for (int i = 0; i < 15; i++) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                shadow.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 冥罗网减速区演出弹幕 (纯视觉, damage=0): 命中瞬间在敌群中心展开 ArenaRunic 冥府罗网法阵地纹 (青蓝),
    /// 标识 300 范围寂灭减速区, 配冲击环 + 径向辉光。绘制只在 PreDraw。
    /// </summary>
    public class NetherNetField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 54;
        private const float ZoneRadius = 300f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<NetherNetField>(), 0, 0f, owner);
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
            Lighting.AddLight(Projectile.Center, 0.3f, 0.8f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.18f ? life / 0.18f : 1f - (life - 0.18f) / 0.82f, 0f, 1f);
            Color primary = new Color(110, 240, 255);
            Color secondary = new Color(30, 110, 160);
            SpriteBatch sb = Main.spriteBatch;

            // —— ArenaRunic 冥罗网法阵地纹 (扩张展开到减速区半径) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                float radius = ZoneRadius * (0.5f + life * 0.5f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fade * 0.8f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(12f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 16f + life * 200f, 12f, fade * 0.8f, primary, secondary);
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.07f, fade * 0.6f, primary, 8f);

            return false;
        }
    }
}

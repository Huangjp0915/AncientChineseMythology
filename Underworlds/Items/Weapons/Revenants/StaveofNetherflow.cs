using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 黄泉幽冥杖 - 凝聚黄泉幽冥之力的法杖，魔法杖类武器
    /// 肉后中期，释放幽冥能量弹，命中后产生幽冥漩涡持续伤害
    /// </summary>
    public class StaveofNetherflow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 52;
            Item.crit = 4;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.width = 42;
            Item.height = 42;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherflowOrb>();
            Item.shootSpeed = 10f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //从杖尖释放幽冥能量弹
            Vector2 staffTip = player.Center + velocity.SafeNormalize(Vector2.Zero) * 50f;
            Projectile.NewProjectile(source, staffTip, velocity, type, damage, knockback, player.whoAmI);

            //施法粒子
            for (int i = 0; i < 8; i++) {
                Vector2 vel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(30)) * Main.rand.NextFloat(2f, 5f);
                Dust cast = Dust.NewDustPerfect(
                    staffTip, DustID.Wraith, vel,
                    120, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                cast.noGravity = true;
            }

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽冥能量弹弹幕 - 缓慢飞行的幽冥能量球，命中后在敌人位置生成幽冥漩涡
    /// 使用ACMAsset.SoftGlow叠加光球效果，ACMAsset.SlashBurst绘制命中爆发
    /// </summary>
    public class NetherflowOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/StaveofNetherflow";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.1f;

            //轻微追踪
            if (Timer > 20f) {
                NPC target = FindClosestNPC(300f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.025f);
                }
            }

            //幽冥蓝绿色光照
            float pulse = 0.5f + MathF.Sin(Timer * 0.15f) * 0.15f;
            Lighting.AddLight(Projectile.Center, 0.2f * pulse, 0.5f * pulse, 0.6f * pulse);

            //幽魂漩涡粒子
            if (Main.rand.NextBool(2)) {
                float angle = Timer * 0.3f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust vortex = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Wraith,
                    -offset.X * 0.1f, -offset.Y * 0.1f,
                    120, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                vortex.noGravity = true;
            }

            //暗影拖尾
            if (Main.rand.NextBool(3)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    150, default, Main.rand.NextFloat(0.7f, 1.1f)
                );
                trail.noGravity = true;
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
            //附加冥府减益
            target.AddBuff(BuffID.ShadowFlame, 180);
            target.AddBuff(BuffID.Slow, 120);

            //命中爆发：幽冥漩涡
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi / 20f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(4f, 7f);
                Dust vortex = Dust.NewDustPerfect(
                    target.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                vortex.noGravity = true;
            }

            //暗影焰环
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust ring = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.3f, 1.8f)
                );
                ring.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //使用SoftGlow绘制幽冥能量球
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;

                //拖尾光球
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(30, 80, 100), new Color(80, 200, 220), progress) * progress * 0.4f;
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, drawPos, null, trailColor, 0f, glowOrigin, 0.5f * progress, SpriteEffects.None, 0);
                }

                //主体光球（双层呼吸光效）
                float pulse1 = 0.7f + MathF.Sin(Timer * 0.15f) * 0.1f;
                Color innerGlow = new Color(80, 220, 240) * 0.6f;
                innerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, innerGlow, 0f, glowOrigin, pulse1, SpriteEffects.None, 0);

                float pulse2 = 0.9f + MathF.Sin(Timer * 0.1f + 1f) * 0.15f;
                Color outerGlow = new Color(40, 120, 160) * 0.3f;
                outerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, outerGlow, 0f, glowOrigin, pulse2, SpriteEffects.None, 0);
            }

            //使用Smoke纹理叠加幽冥烟雾感（取一帧）
            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frame = (int)(Timer * 0.3f) % 16;
                int frameX = frame % 4;
                int frameY = frame / 4;
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Rectangle sourceRect = new Rectangle(frameX * frameW, frameY * frameH, frameW, frameH);
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);

                Color smokeColor = new Color(60, 150, 180) * 0.2f;
                smokeColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, smokeColor, Timer * 0.05f, smokeOrigin, 0.15f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //使用SlashBurst叠加消亡爆发效果
            Texture2D slashBurst = ACMAsset.SlashBurst;
            if (slashBurst != null) {
                //SlashBurst的视觉效果通过粒子模拟
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);

            //幽冥爆散
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust death = Dust.NewDustPerfect(
                    Projectile.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                death.noGravity = true;
            }

            //暗影焰碎片
            for (int i = 0; i < 8; i++) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                shadow.noGravity = true;
            }
        }
    }
}

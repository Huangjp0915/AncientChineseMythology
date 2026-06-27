using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
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

            //命中: 幽冥漩涡演出 (径向辉光 + 冲击环, 代偿 GenericWarp 局部漩涡扭曲, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 1.15f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            //幽冥拖尾 (双层 ribbon: 外宽暗蓝 + 内窄亮青)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 13f,
                outerColor: new Color(30, 90, 130, 150), innerColor: new Color(110, 235, 250, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            //幽冥旋涡烟雾 (取一帧 Smoke 旋转叠加)
            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frame = (int)(Timer * 0.3f) % 16;
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Rectangle sourceRect = new Rectangle((frame % 4) * frameW, (frame / 4) * frameH, frameW, frameH);
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);
                Color smokeColor = new Color(50, 140, 175) * 0.22f;
                smokeColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, smokeColor, Timer * 0.06f, smokeOrigin, 0.22f, SpriteEffects.None, 0);
            }

            //双层能量球: 外宽暗 + 内窄亮 (呼吸脉动)
            float pulse = 0.5f + MathF.Sin(Timer * 0.15f) * 0.12f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.7f + pulse * 0.6f), new Color(35, 110, 150) * 0.5f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (0.9f + pulse * 0.4f), new Color(120, 235, 250) * 0.85f);

            //核心径向辉光 (RadialBloom 双层弹芯, 占全屏名额, 名额满退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, radiusFrac: 0.045f, intensity: 0.4f,
                color: new Color(110, 230, 250), rayCount: 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消亡爆发: 黄泉幽冥径向辉光 + 冲击环 (ACMWeaponBurst 暗冥幽蓝紫)
            ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
                ACMWeaponBurst.AbyssPurple, 1.2f, Projectile.owner);

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

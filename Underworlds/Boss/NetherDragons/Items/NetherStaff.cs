using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥之杖 - 魔法武器
    /// 放出穿梭于敌群间的幽冥怨魂，命中叠加「幽冥怨念」并不时迸射追魂火星。
    /// </summary>
    internal class NetherStaff : ModItem
    {
        private const int MaxWisps = 4;

        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 135;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item125 with { Pitch = 0.2f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<NetherOrbProjectile>();
            Item.shootSpeed = 11f;
            Item.mana = 12;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 维持上限，超出则回收最早的怨魂
            int count = player.ownedProjectileCounts[type];
            if (count >= MaxWisps) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == player.whoAmI && p.type == type) {
                        p.Kill();
                        break;
                    }
                }
            }

            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir * Item.shootSpeed, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<UmbralStoneItem>(), 15)
                .AddIngredient(ModContent.ItemType<NetherBar>(), 10)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽冥怨魂 - 在敌群间穿梭追击，叠加怨念并迸射追魂火星的弹幕。
    /// </summary>
    public class NetherOrbProjectile : ModProjectile
    {
        // 占位弹幕改纯着色器自绘 (附录 B), 保留原版 1 号空贴图。
        public override string Texture => "Terraria/Images/Projectile_1";

        private ref float SparkTimer => ref Projectile.localAI[0];
        private ref float Pulse => ref Projectile.localAI[1];

        /// <summary>当前追猎目标的怨念归一化深度 (0~1), 驱动青蓝冥焰的狂暴度。</summary>
        private float grudge01;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 280;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void AI() {
            Pulse += 0.18f;
            SparkTimer++;

            NPC target = FindTarget(820f);
            float targetGrudge = 0f;
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 15f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.07f);
                targetGrudge = target.GetGlobalNPC<NetherGrudgeGlobalNPC>().Stacks / (float)NetherGrudgeGlobalNPC.MaxStacks;

                // 周期性向另一名敌人迸射追魂火星
                if (SparkTimer >= 26f && Projectile.owner == Main.myPlayer) {
                    SparkTimer = 0f;
                    NPC sparkTarget = FindTarget(560f) ?? target;
                    Vector2 sv = (sparkTarget.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedByRandom(0.25) * 8f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, sv,
                        ModContent.ProjectileType<NetherSoulSpark>(), Math.Max(1, (int)(Projectile.damage * 0.45f)),
                        0f, Projectile.owner);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
                }
            }
            else {
                Projectile.velocity *= 0.97f;
            }

            // 怨念深度平滑跟随 (绘制阶段用)
            grudge01 = MathHelper.Lerp(grudge01, targetGrudge, 0.08f);

            // 灵魂飘动
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Pulse) * 0.012f);
            Projectile.rotation += 0.1f;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    -Projectile.velocity * 0.1f, 100, default, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, NetherFX.Mix(0.5f).ToVector3() * (0.8f + grudge01 * 0.6f));
        }

        private NPC FindTarget(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NetherGrudgeGlobalNPC.AddGrudge(target, 2, Projectile);
            NetherFX.SoulDust(target.Center, 5f, 6, 1.3f);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.3f, Volume = 0.5f }, target.Center);
            // 命中演出走一次性 burst (更新阶段禁止直接绘制 — IRON RULE 1)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 0.75f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float pulse = 0.7f + MathF.Sin(Pulse) * 0.18f;
            float rage = grudge01;

            // 1) 穿梭残影: 双层 ribbon 拖尾 (外宽暗冥 + 内窄青焰), 流动 UV; 怨念越深越宽越紫
            Color outer = Color.Lerp(NetherFX.Deep, NetherFX.Violet, rage); outer.A = 150;
            Color inner = Color.Lerp(NetherFX.Cyan, Color.White, 0.25f + rage * 0.35f); inner.A = 210;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 13f + rage * 9f,
                outerColor: outer, innerColor: inner, tex: ACMAsset.SoftGlow,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            // 2) 怨魂核心柔光闪
            WeaponVFX.DrawGlowBurst(Projectile.Center, (0.85f + rage * 0.55f) * pulse,
                Color.Lerp(NetherFX.Cyan, NetherFX.Violet, 0.5f + 0.3f * MathF.Sin(Pulse)));

            // 3) wisp 径向辉光核 (占全屏名额, 满名额自动退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.05f + rage * 0.035f,
                (0.38f + rage * 0.42f) * pulse, NetherFX.Cyan, 6f);

            // 4) 满怨念 → PaletteLUT 提亮 (处决/引爆定调, 仅满层时短促占名额, 强度 ≤0.15)
            if (rage >= 0.85f)
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                    new Color(20, 55, 120), new Color(150, 235, 255), 0.1f, saturation: 1.12f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f }, Projectile.Center);
            NetherFX.SoulDust(Projectile.Center, 8f, 18, 1.6f);
        }
    }

    /// <summary>
    /// 追魂火星 - 怨魂迸射的小型追踪弹，命中叠加怨念。
    /// </summary>
    public class NetherSoulSpark : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            NPC target = null;
            float best = 460f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    target = npc;
                }
            }
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 13f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
            }
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, NetherFX.Cyan.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NetherGrudgeGlobalNPC.AddGrudge(target, 1, Projectile);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 细窄青焰双层拖尾 + 火星核
            Color outer = NetherFX.Deep; outer.A = 140;
            Color inner = Color.Lerp(NetherFX.Cyan, Color.White, 0.3f); inner.A = 200;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 6f,
                outerColor: outer, innerColor: inner, tex: ACMAsset.SoftGlow,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, Color.Lerp(NetherFX.Cyan, Color.White, 0.35f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            NetherFX.SoulDust(Projectile.Center, 4f, 6, 1f);
        }
    }
}

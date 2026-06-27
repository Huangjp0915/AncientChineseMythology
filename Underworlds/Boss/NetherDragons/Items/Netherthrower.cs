using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥喷射器 - 远程武器
    /// 喷出会自行追猎的幽冥龙息，密集的灼烧迅速叠满「幽冥怨念」引爆灵魂湮灭。
    /// </summary>
    internal class Netherthrower : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 95;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 64;
            Item.height = 32;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item34 with { Pitch = -0.2f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<NetherBreathProjectile>();
            Item.shootSpeed = 11f;
            Item.noMelee = true;
            Item.useAmmo = AmmoID.Gel;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<NetherBreathProjectile>();
            velocity = velocity.RotatedByRandom(0.16f) * Main.rand.NextFloat(0.85f, 1.15f);
            position += velocity.SafeNormalize(Vector2.Zero) * 48f;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-10f, -2f);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<UmbralStoneItem>(), 10)
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽冥龙息 - 会缓缓追猎敌人的灵焰弹幕。
    /// </summary>
    public class NetherBreathProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int MaxLife = 38;
        private float LifeProgress => 1f - Projectile.timeLeft / (float)MaxLife;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            // 龙息自行追猎最近的敌人
            NPC target = null;
            float best = 280f;
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
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.08f);
            }

            Projectile.velocity *= 0.985f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    Projectile.velocity * 0.2f, 120, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, NetherFX.Mix(LifeProgress).ToVector3() * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 150);
            NetherGrudgeGlobalNPC.AddGrudge(target, 1, Projectile);
            // 灼杀引爆演出 (致命命中, 更新阶段禁止直接绘制 — IRON RULE 1)
            if (target.life <= 0 && Projectile.owner == Main.myPlayer)
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, scale: 1.1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = LifeProgress;
            // 火焰先膨胀再消散
            float size = MathHelper.Lerp(0.35f, 0.95f, MathF.Min(1f, life * 3f)) * (1f - life * 0.4f);

            // 连续龙息: 沿travel的 BeamGrad 流动束 (高频弹幕叠出连贯火舌)
            Vector2 tail = Projectile.Center;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] != Vector2.Zero) {
                    tail = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    break;
                }
            }
            Color core = Color.Lerp(Color.White, NetherFX.Cyan, 0.4f);
            Color edge = Color.Lerp(NetherFX.Cyan, NetherFX.Violet, life);
            ACMShaders.DrawBeam(tail, Projectile.Center, 12f + size * 18f, core, edge,
                1f - life * 0.5f, flowSpeed: 2.6f, flowScale: 1.6f, coreSharp: 1.8f);

            // 火舌核心柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, size * 1.05f,
                Color.Lerp(Color.White, NetherFX.Cyan, 0.3f) * (1f - life * 0.5f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            NetherFX.SoulDust(Projectile.Center, 4f, 8, 1.2f);
        }
    }
}

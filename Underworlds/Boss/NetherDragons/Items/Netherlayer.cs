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
    /// 幽冥雍刀 - 近战武器
    /// 挥砍掷出幽冥新月斩；每第三击爆发更宽的裂魂斩，直击与斩波都会叠加「幽冥怨念」。
    /// </summary>
    internal class Netherlayer : ModItem
    {
        private int comboCounter;
        private int comboTimer;

        public override void SetDefaults() {
            Item.damage = 180;
            Item.DamageType = DamageClass.Melee;
            Item.width = 88;
            Item.height = 88;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.shoot = ModContent.ProjectileType<NetherSlashProjectile>();
            Item.shootSpeed = 14f;
        }

        public override void HoldItem(Player player) {
            if (comboTimer > 0 && --comboTimer == 0)
                comboCounter = 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            comboCounter++;
            comboTimer = 50;
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);

            bool empowered = comboCounter % 3 == 0;
            if (empowered) {
                Projectile.NewProjectile(source, player.Center, dir * Item.shootSpeed * 0.9f,
                    type, (int)(damage * 1.35f), knockback * 1.4f, player.whoAmI, 1f);
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f }, player.Center);
            }
            else {
                Projectile.NewProjectile(source, player.Center, dir * Item.shootSpeed,
                    type, damage, knockback, player.whoAmI, 0f);
            }
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                int type = Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(hitbox.TopLeft(), hitbox.Width, hitbox.Height, type, 0, 0, 100, default, 1.4f);
                d.noGravity = true;
                d.velocity = player.velocity * 0.4f;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 直击灌注怨念
            NetherGrudgeGlobalNPC.AddGrudge(target, 2, player, Item.damage);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<UmbralStoneItem>(), 12)
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽冥新月斩 - 飞行的弧形斩波，命中叠加怨念。
    /// </summary>
    public class NetherSlashProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private bool Empowered => Projectile.ai[0] > 0.5f;
        private ref float Pulse => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Pulse += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Empowered) {
                Projectile.velocity *= 1.012f;
                if (Projectile.timeLeft == 54)
                    Projectile.timeLeft = 70;
            }
            else {
                Projectile.velocity *= 0.99f;
            }

            if (Main.rand.NextBool(2)) {
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Vector2 pos = Projectile.Center + perp * Main.rand.NextFloat(-26f, 26f);
                Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    -Projectile.velocity * 0.06f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, NetherFX.Mix(0.5f).ToVector3() * (Empowered ? 1f : 0.7f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NetherGrudgeGlobalNPC.AddGrudge(target, Empowered ? 3 : 2, Projectile);
            NetherFX.SoulDust(target.Center, 6f, Empowered ? 12 : 7, 1.4f);
            if (Empowered)
                target.AddBuff(BuffID.ShadowFlame, 180);
            // 命中演出 (更新阶段禁止直接绘制 — IRON RULE 1); 裂魂斩更大规模
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: Empowered ? 1.25f : 0.8f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float maxLife = Empowered ? 70f : 55f;
            float fade = MathHelper.Clamp(Projectile.timeLeft / maxLife, 0f, 1f);

            // 1) 新月斩弧形拖尾 (GlaciateWave 剑气片, 外宽暗冥 + 内窄青焰)
            Color outer = (Empowered ? NetherFX.Violet : NetherFX.Deep); outer.A = (byte)(150 * fade);
            Color inner = Color.Lerp(NetherFX.Cyan, Color.White, 0.4f); inner.A = (byte)(215 * fade);
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: Empowered ? 34f : 22f,
                outerColor: outer, innerColor: inner, tex: ACMAsset.GlaciateWave,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f, subdivisions: 4);

            // 2) BeamGrad 裂魂斩核 (沿运动方向一道流动光束)
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float len = Empowered ? 78f : 52f;
            ACMShaders.DrawBeam(Projectile.Center - dir * len, Projectile.Center + dir * len * 0.45f,
                Empowered ? 17f : 10f, Color.Lerp(NetherFX.Cyan, Color.White, 0.5f), NetherFX.Violet,
                fade, flowSpeed: 2.2f);

            // 3) 第三击宽幅裂魂斩爆闪 (仅蓄力斩, 仅生成早期一闪)
            if (Empowered && Projectile.timeLeft > 60)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.13f, 0.7f, NetherFX.Cyan, 10f);

            // 4) 斩心柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, (Empowered ? 1.1f : 0.7f) * fade,
                Color.Lerp(Color.White, NetherFX.Cyan, 0.4f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.5f }, Projectile.Center);
            NetherFX.SoulDust(Projectile.Center, 7f, Empowered ? 20 : 12, 1.5f);
        }
    }
}

using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 冥骨弓 - 由地府亡灵骨骼制成的弓，远程弓类武器。
    /// 重做"引魂齐射"：移除 1/3 随机额外箭，改为固定节奏 —— 每第 4 射为引魂齐射
    /// （3 支无重力强追踪的魂骨箭扇形射出）；第 1-3 发骨磬音高阶梯上行, 节奏可听。
    /// </summary>
    public class NetherboneBow : ModItem
    {
        /// <summary>射击计数（第 4 发引魂齐射, owner 端节奏）。</summary>
        internal int shotCounter;
        private const int VolleyEvery = 4;

        public override void SetDefaults() {
            Item.damage = 42;
            Item.crit = 6;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 24;
            Item.height = 56;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.value = Item.buyPrice(gold: 4, silver: 50);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = null; //骨磬音高阶梯由 Shoot 播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            shotCounter++;
            bool volley = shotCounter >= VolleyEvery;

            if (volley) {
                shotCounter = 0;
                //—— 引魂齐射: 3 支无重力强追踪魂骨箭扇形射出 ——
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = velocity.RotatedBy(MathHelper.ToRadians(9f * i)) * 1.1f;
                    Projectile.NewProjectile(source, position, vel,
                        ModContent.ProjectileType<NetherboneSoulArrow>(), damage, knockback, player.whoAmI);
                }
                //低音齐鸣 (骨鼓) + 冥火喷薄
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.9f, Pitch = -0.35f }, player.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.45f, Pitch = 0.1f }, player.Center);
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(position + velocity.SafeNormalize(Vector2.UnitX) * 14f, DustID.IceTorch,
                        velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 5f),
                        100, default, Main.rand.NextFloat(1f, 1.5f));
                    d.noGravity = true;
                }
            }
            else {
                //普通冥火骨箭 (骨磬音高阶梯: 齐射临近可听)
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<NetherboneArrow>(), damage, knockback, player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.75f, Pitch = -0.05f + shotCounter * 0.12f }, player.Center);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 冥火骨箭 - 复用原版木箭 AI (重力/插地弹道), 冥蓝-骨白双层冥火表现。
    /// 无新 PNG: 箭体沿用原版木箭贴图, 视觉全部叠在 PreDraw。
    /// </summary>
    public class NetherboneArrow : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.aiStyle = ProjAIStyleID.Arrow;
            AIType = ProjectileID.WoodenArrowFriendly;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.18f, 0.32f, 0.4f);
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.IceTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 110, default, 1.1f);
                d.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 0.8f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return true;

            //冥蓝-骨白双层冥火拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(30, 90, 150, 150), innerColor: new Color(210, 235, 255, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            //箭尖骨白柔光闪 (廉价, 不占名额)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.55f, new Color(150, 210, 245));
            return true; //保留原版箭体
        }
    }

    /// <summary>
    /// 魂骨箭 - 引魂齐射专属：无重力、强追踪、骨白亡魂箭体（程序化, 无 PNG）。
    /// "亡魂替你引弓认路"——追踪转向大, 拖尾更亮更长。
    /// </summary>
    public class NetherboneSoulArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.NetherboneSoulArrow.DisplayName",
                () => "Soulbone Arrow");
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.light = 0.35f;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //强追踪 (无重力 — 亡魂引路)
            NPC target = FindTarget(700f);
            if (target != null) {
                float speed = MathF.Max(Projectile.velocity.Length(), 10f);
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 dir = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), toTarget, 0.14f);
                Projectile.velocity = dir.SafeNormalize(Vector2.UnitX) * speed;
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.42f, 0.5f);
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.IceTorch : DustID.Bone,
                    -Projectile.velocity * 0.08f, 120, default, 0.9f);
                d.noGravity = true;
            }
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 150);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Bone, scale: 0.9f, owner: Projectile.owner);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Bone,
                    Main.rand.NextVector2Circular(3f, 3f), 90, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //骨白-冥蓝加亮拖尾 (齐射箭的"更亮"视觉分级)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 12f,
                outerColor: new Color(60, 130, 190, 170), innerColor: new Color(235, 245, 255, 220),
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            //程序化箭体: 骨白亡魂矢 (BlankStar 拉长为箭杆 + 柔光头)
            Vector2 pos = Projectile.Center - Main.screenPosition;
            var star = ACMAsset.BlankStar;
            if (star != null) {
                Main.spriteBatch.Draw(star, pos, null, new Color(235, 245, 255, 0), Projectile.rotation,
                    star.Size() * 0.5f, new Vector2(0.1f, 0.32f), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            }
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.65f, new Color(190, 225, 255));
            return false;
        }
    }
}

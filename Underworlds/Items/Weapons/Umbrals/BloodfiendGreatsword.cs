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
    /// 血魔巨剑 - 地府血魔锻造的巨剑，近战大剑。
    /// 重做"血怨三段"：斩1 → 斩2 → 重斩（放大 0.8x 速·1.4x 伤·释放短程血浪）。
    /// 数值重整：原 155伤@12帧 (名义 775 DPS) 为本梯队均值 5.5 倍，属遗留错误；
    /// 重整为 110伤@24帧 三段结构（论证见 Docs/WeaponRedo/Umbrals.md §6）。
    /// 吸血保留：5% 转生命（暴击双倍），血丝回流可见。
    /// </summary>
    public class BloodfiendGreatsword : ModItem
    {
        /// <summary>连击步 1→2→3(重斩)，超时回位。</summary>
        internal int comboStep;
        internal int comboIdleTimer;
        /// <summary>本次挥舞是否重斩（CanUseItem 时锁定, 供挥速/体积/伤害/血浪判定）。</summary>
        internal bool heavySwing;

        public override void SetDefaults() {
            Item.damage = 110;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.value = Item.buyPrice(gold: 5, silver: 50);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.scale = 1.2f;
            Item.shoot = ModContent.ProjectileType<BloodfiendCrimsonWave>();
            Item.shootSpeed = 11f;
        }

        public override bool CanUseItem(Player player) {
            //连击推进: 每次挥舞判定本挥是否为第 3 段重斩
            comboStep = comboStep % 3 + 1;
            comboIdleTimer = 0;
            heavySwing = comboStep == 3;
            if (heavySwing) {
                //重斩起手: 低沉蓄势音
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.45f }, player.Center);
            }
            return true;
        }

        public override float UseSpeedMultiplier(Player player) {
            //重斩更慢更沉 (速度对比 = 重量感)
            return heavySwing ? 0.8f : 1f;
        }

        public override void ModifyItemScale(Player player, ref float scale) {
            if (heavySwing)
                scale += 0.25f;
        }

        public override void UpdateInventory(Player player) {
            //1 秒不挥回位 (连击不跨战斗残留)
            if (comboStep > 0 && ++comboIdleTimer > 60) {
                comboStep = 0;
                heavySwing = false;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //仅重斩释放血浪 (0.8x 短程穿透)
            if (heavySwing) {
                Projectile.NewProjectile(source, player.Center, velocity,
                    ModContent.ProjectileType<BloodfiendCrimsonWave>(), (int)(damage * 0.8f), knockback * 0.7f,
                    player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            }
            return false;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            //重斩 1.4x
            if (heavySwing)
                modifiers.FinalDamage *= 1.4f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //血魔吸血：伤害 5% 转生命 (暴击双倍)
            int healAmount = (int)(damageDone * 0.05f);
            if (healAmount > 0) {
                player.Heal(healAmount);
            }
            if (hit.Crit && healAmount > 0) {
                player.Heal(healAmount);
            }

            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: heavySwing ? 1.5f : (hit.Crit ? 1.3f : 1.0f), owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, heavySwing ? 4.5f : (hit.Crit ? 3.5f : 2.2f));

            //吸血可见化：血丝从命中点牵引回玩家 (仅本地玩家生成纯视觉弹幕)
            if (healAmount > 0 && Main.myPlayer == player.whoAmI) {
                int threads = hit.Crit || heavySwing ? 2 : 1;
                for (int i = 0; i < threads; i++) {
                    Projectile.NewProjectile(player.GetSource_OnHit(target),
                        target.Center + Main.rand.NextVector2Circular(18f, 18f), Vector2.Zero,
                        ModContent.ProjectileType<BloodfiendLifestealThread>(), 0, 0f, player.whoAmI);
                }
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞血红色粒子 (重斩更密)
            if (Main.rand.NextBool(heavySwing ? 1 : 2)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Blood, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 150, default,
                    heavySwing ? 1.8f : 1.4f);
                d.noGravity = heavySwing;
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), heavySwing ? 0.7f : 0.5f, 0.06f, 0.08f);
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 血浪 - 重斩释放的短程穿透血弧（程序化: 新月形 ribbon + 血珠飞溅）。
    /// 短命 (26f) 快衰减 — 只作重斩的"余势", 不抢走剑本体的主角地位。
    /// </summary>
    public class BloodfiendCrimsonWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 26;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.BloodfiendCrimsonWave.DisplayName",
                () => "Crimson Wave");
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            //血浪渐衰 (硬发射软收尾)
            Projectile.velocity *= 0.94f;

            if (Main.rand.NextBool(2)) {
                Vector2 perp = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Dust d = Dust.NewDustPerfect(Projectile.Center + perp * Main.rand.NextFloat(-24f, 24f),
                    DustID.Blood, Projectile.velocity * 0.3f, 120, default, Main.rand.NextFloat(1.2f, 1.7f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.45f, 0.05f, 0.08f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //血浪命中亦回流吸血线 (5% 同规则, owner 端)
            Player owner = Main.player[Projectile.owner];
            int healAmount = (int)(damageDone * 0.05f);
            if (healAmount > 0) {
                owner.Heal(healAmount);
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target),
                        target.Center + Main.rand.NextVector2Circular(14f, 14f), Vector2.Zero,
                        ModContent.ProjectileType<BloodfiendLifestealThread>(), 0, 0f, Projectile.owner);
                }
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 1.0f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float lifeT = 1f - Projectile.timeLeft / (float)LifeTime;
            float fade = 1f - lifeT;

            //新月血弧: 垂直于飞行方向的短 ribbon (三点弯月)
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float span = 44f * (0.8f + lifeT * 0.5f);
            Vector2[] arc = [
                Projectile.Center + perp * span - dir * 14f,
                Projectile.Center + dir * 12f,
                Projectile.Center - perp * span - dir * 14f,
            ];
            WeaponVFX.DrawRibbonTrail(arc, 16f * fade + 4f,
                new Color(120, 10, 14, (int)(170 * fade)), new Color(255, 80, 80, (int)(220 * fade)),
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f, subdivisions: 4);

            //飞行拖尾 (暗血)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 12f * fade,
                outerColor: new Color(90, 8, 12, 130), innerColor: new Color(220, 60, 60, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.8f);

            //浪头血珠柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f * fade, new Color(220, 40, 50) * fade);
            return false;
        }
    }

    /// <summary>
    /// 血魔吸血血丝 - 纯视觉一次性弹幕：自命中点向玩家回流的 BeamGrad 血弧线 (吸血可见化)。
    /// 不造成伤害, ShouldUpdatePosition=false (锚在命中点), 用 BeamGrad 画一条收向玩家的血色光束。
    /// </summary>
    public class BloodfiendLifestealThread : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 16;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Player owner = Main.player[Projectile.owner];
            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            //血丝起点向玩家收束 (回流感)
            Vector2 head = Vector2.Lerp(Projectile.Center, owner.Center, life * life);
            float intensity = 1f - life;

            ACMShaders.DrawBeam(head, owner.Center, halfWidth: MathHelper.Lerp(5f, 1.5f, life),
                core: new Color(255, 90, 90, 200), edge: new Color(120, 10, 12, 0), intensity: intensity,
                flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

            //尾端血珠柔光
            WeaponVFX.DrawGlowBurst(head, 0.6f * intensity, new Color(220, 40, 50) * intensity);
            return false;
        }
    }
}

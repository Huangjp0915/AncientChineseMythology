using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 冥骨弓 - 由地府亡灵骨骼制成的弓，远程弓类武器
    /// 肉后初期，把箭矢转化为自绘"冥火骨箭" <see cref="NetherboneArrow"/> 射出 (仍消耗箭矢, 弹道不变)。
    /// 可见质变: 冥蓝-骨白双层冥火拖尾 + 命中冥蓝魂火辉光演出 (取代原版地狱火箭)。
    /// </summary>
    public class NetherboneBow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 42; //基础伤害
            Item.crit = 6; //暴击率
            Item.DamageType = DamageClass.Ranged; //远程伤害类型
            Item.width = 24; //物品宽度
            Item.height = 56; //物品高度
            Item.useTime = 22; //使用时间
            Item.useAnimation = 22; //使用动画时间
            Item.useStyle = ItemUseStyleID.Shoot; //射击风格
            Item.knockBack = 2.5f; //击退
            Item.value = Item.buyPrice(gold: 4, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item5; //弓箭声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ProjectileID.WoodenArrowFriendly; //默认发射木箭
            Item.shootSpeed = 10f; //弹幕速度
            Item.useAmmo = AmmoID.Arrow; //使用箭矢弹药
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0); //手持位置微调
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //把任意箭矢转化为冥火骨箭 (保留弹道/伤害)
            int boneArrow = ModContent.ProjectileType<NetherboneArrow>();
            Projectile.NewProjectile(source, position, velocity, boneArrow, damage, knockback, player.whoAmI);

            //发射时有几率发射额外一支箭
            if (Main.rand.NextBool(3)) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(8));
                Projectile.NewProjectile(source, position, perturbedSpeed, boneArrow, damage, knockback, player.whoAmI);
            }
            return false; //不发射默认箭 (已手动生成)
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 冥火骨箭 - 复用原版木箭 AI (重力/插地弹道), 升级为冥蓝-骨白双层冥火表现。
    /// 无新 PNG: 箭体沿用原版木箭贴图, 视觉全部叠在 PreDraw (冥火拖尾 + 骨白柔光)。
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
            //冥蓝魂火光照 + 稀疏冥火粒子
            Lighting.AddLight(Projectile.Center, 0.18f, 0.32f, 0.4f);
            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //保留原版地狱火箭"点燃"机制等价 (改为暗影焰冥火)
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
}

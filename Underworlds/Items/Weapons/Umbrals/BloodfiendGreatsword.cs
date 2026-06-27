using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 血魔巨剑 - 地府血魔锻造的巨剑，近战大剑类武器
    /// 肉后初期，攻击吸血，范围较大
    /// 重做：把吸血做成可见的血丝牵引 —— 命中回血时生成 <see cref="BloodfiendLifestealThread"/>
    /// 由命中点向玩家回流的 BeamGrad 血弧线 + 致命红命中辉光。
    /// </summary>
    public class BloodfiendGreatsword : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 155; //基础伤害
            Item.crit = 8; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 64; //物品宽度（大剑较大）
            Item.height = 64; //物品高度
            Item.useTime = 12; //使用时间（大剑较慢）
            Item.useAnimation = 12; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 5.5f; //击退
            Item.value = Item.buyPrice(gold: 5, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.scale = 1.2f; //放大显示
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //血魔吸血：造成伤害的5%转化为生命
            int healAmount = (int)(damageDone * 0.05f);
            if (healAmount > 0) {
                player.Heal(healAmount);
            }
            //暴击时额外吸血
            if (hit.Crit) {
                player.Heal(healAmount);
            }

            //命中血色辉光演出
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: hit.Crit ? 1.3f : 1.0f, owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, hit.Crit ? 3.5f : 2.2f);

            //吸血可见化：血丝从命中点牵引回玩家 (仅本地玩家生成纯视觉弹幕)
            if (healAmount > 0 && Main.myPlayer == player.whoAmI) {
                int threads = hit.Crit ? 2 : 1;
                for (int i = 0; i < threads; i++) {
                    Projectile.NewProjectile(player.GetSource_OnHit(target),
                        target.Center + Main.rand.NextVector2Circular(18f, 18f), Vector2.Zero,
                        ModContent.ProjectileType<BloodfiendLifestealThread>(), 0, 0f, player.whoAmI);
                }
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生血红色粒子
            if (Main.rand.NextBool(2)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Blood, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 150, default, 1.4f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.5f, 0.06f, 0.08f);
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
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

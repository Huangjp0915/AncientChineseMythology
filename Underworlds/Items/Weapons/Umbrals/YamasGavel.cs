using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 阎罗锤 - 地府阎王的审判之锤，可投掷并飞回的回旋锤
    /// 肉后初期，高击退，黄色+地狱烈焰风格
    /// </summary>
    public class YamasGavel : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 58; //基础伤害
            Item.crit = 4; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 48; //物品宽度
            Item.height = 48; //物品高度
            Item.useTime = 28; //使用时间
            Item.useAnimation = 28; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 8f; //高击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不使用近战碰撞
            Item.noUseGraphic = true; //隐藏使用图形，因为我们用投射物
            Item.shoot = ModContent.ProjectileType<YamasGavelProjectile>(); //发射阎罗锤投射物
            Item.shootSpeed = 14f; //投掷速度
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //计算投掷方向
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            direction = direction.RotatedByRandom(MathHelper.ToRadians(2f));

            //创建阎罗锤投射物
            Projectile.NewProjectileDirect(
                source,
                player.Center + direction * 30f,
                direction * Item.shootSpeed,
                type,
                damage,
                knockback,
                player.whoAmI
            );

            //播放投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.9f,
                Pitch = -0.2f
            }, player.Center);

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 阎罗锤投射物 - 投掷后旋转飞行，到达最远距离后飞回玩家
    /// 黄色+地狱烈焰特效
    /// </summary>
    public class YamasGavelProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Umbrals/YamasGavel";

        //AI状态
        private enum GavelState { Flying, Returning }
        private GavelState State {
            get => (GavelState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private const float MaxDistance = 450f; //最大飞行距离
        private const float ReturnSpeed = 18f; //返回速度
        private float spinSpeed = 0.3f; //旋转速度

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; //无限穿透
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Timer++;

            //旋转锤子
            Projectile.rotation += spinSpeed * Projectile.direction;

            switch (State) {
                case GavelState.Flying:
                    HandleFlying(owner);
                    break;
                case GavelState.Returning:
                    HandleReturning(owner);
                    break;
            }

            //生成黄色+烈焰粒子
            SpawnFlameParticles();

            //光照效果（黄色+橙红色）
            Lighting.AddLight(Projectile.Center, 1f, 0.7f, 0.2f);
        }

        private void HandleFlying(Player owner) {
            //减速
            Projectile.velocity *= 0.97f;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.5f, 0.02f);

            //检查是否达到最大距离或速度过低
            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 2f || Timer > 45) {
                State = GavelState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = -0.1f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            //加速飞回玩家
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);

            //动态返回速度
            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.5f, 1f - distance / MaxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.15f);

            //加速旋转
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.6f, 0.05f);

            //接近玩家时消失
            if (distance < 35f) {
                Projectile.Kill();
            }
        }

        private void SpawnFlameParticles() {
            //黄色火焰粒子
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 16,
                    32, 32,
                    DustID.Torch,
                    Projectile.velocity.X * 0.2f,
                    Projectile.velocity.Y * 0.2f,
                    100,
                    new Color(255, 200, 50), //黄色
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                flame.noGravity = true;
            }

            //地狱烈焰粒子
            if (Main.rand.NextBool(3)) {
                Dust hellfire = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                    4, 4,
                    DustID.InfernoFork,
                    0, 0,
                    150,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                hellfire.noGravity = true;
                hellfire.velocity = -Projectile.velocity * 0.3f;
            }

            //金色闪光
            if (Main.rand.NextBool(5)) {
                Dust gold = Dust.NewDustDirect(
                    Projectile.Center,
                    4, 4,
                    DustID.GoldFlame,
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-2f, 2f),
                    100,
                    default,
                    1.5f
                );
                gold.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //阎罗审判：附带地狱火效果
            target.AddBuff(BuffID.OnFire, 180); //3秒地狱火

            //有几率造成混乱
            if (Main.rand.NextBool(5)) {
                target.AddBuff(BuffID.Confused, 120); //2秒混乱
            }

            //击中爆发黄金烈焰
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center,
                    DustID.GoldFlame,
                    vel,
                    100,
                    default,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                burst.noGravity = true;
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰到方块不消失，直接返回
            if (State == GavelState.Flying) {
                State = GavelState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f }, Projectile.position);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            //绘制黄金烈焰拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                //黄色到橙红色渐变
                Color trailColor = Color.Lerp(new Color(255, 100, 30), new Color(255, 220, 100), progress) * progress * 0.6f;
                trailColor.A = 0;

                float scale = Projectile.scale * progress;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    origin,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Color mainColor = Color.Lerp(lightColor, new Color(255, 230, 150), 0.4f);
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                mainColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //绘制黄金光晕
            Color glowColor = new Color(255, 200, 50) * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glowColor,
                Projectile.rotation,
                origin,
                Projectile.scale * 1.15f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消失时的烈焰爆发
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.GoldFlame,
                    Main.rand.NextFloat(-4f, 4f),
                    Main.rand.NextFloat(-4f, 4f),
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                death.noGravity = true;
            }
        }
    }
}

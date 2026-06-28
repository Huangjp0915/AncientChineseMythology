using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 索魂匕 - 地府索魂使者的双匕首，投掷/近战武器
    /// 肉后初期，可投掷，攻速快，有几率造成即死（对普通敌人）
    /// </summary>
    public class SoulseekerDaggers : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 38; //基础伤害
            Item.crit = 12; //高暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 32; //物品宽度
            Item.height = 32; //物品高度
            Item.useTime = 15; //快速使用
            Item.useAnimation = 15; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 2f; //低击退
            Item.value = Item.buyPrice(gold: 4, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.shoot = ModContent.ProjectileType<SoulseekerDaggersProj>(); //投掷索魂匕弹幕
            Item.shootSpeed = 16f; //投掷速度
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            //索魂一击：低血量敌人有几率被索命
            if (target.life < target.lifeMax * 0.15f && !target.boss && Main.rand.NextBool(5)) {
                //对非Boss低血量敌人造成致命伤害
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                //产生索魂特效
                for (int i = 0; i < 6; i++) {
                    Dust.NewDust(target.position, target.width, target.height,
                        DustID.Wraith, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.5f);
                }
                SoulseekerSoulChain.SpawnExecute(player.GetSource_OnHit(target), target.Center, player.whoAmI);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //有几率投掷两把匕首
            if (Main.rand.NextBool(3)) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return true;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            //挥舞时产生幽魂粒子
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.Wraith, 0f, 0f, 150, default, 0.8f);
            }
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 索魂匕弹幕 - 投掷后旋转飞行，可穿透敌人，带有幽魂拖尾
    /// </summary>
    public class SoulseekerDaggersProj : ModProjectile
    {
        private ref float Timer => ref Projectile.ai[0];
        private ref float HasHitEnemy => ref Projectile.ai[1]; //记录是否击中过敌人

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3; //可穿透3个敌人
            Projectile.timeLeft = 180; //3秒存在时间
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Timer++;

            //快速旋转
            Projectile.rotation += 0.4f * Projectile.direction;

            //轻微重力
            if (Projectile.velocity.Y < 12f) {
                Projectile.velocity.Y += 0.15f;
            }

            //生成幽魂粒子
            SpawnSoulParticles();

            //幽蓝色光照
            Lighting.AddLight(Projectile.Center, 0.4f, 0.5f, 0.7f);
        }

        private void SpawnSoulParticles() {
            //幽魂拖尾粒子
            if (Main.rand.NextBool(2)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 8,
                    16, 16,
                    DustID.Wraith,
                    Projectile.velocity.X * 0.1f,
                    Projectile.velocity.Y * 0.1f,
                    100,
                    default,
                    Main.rand.NextFloat(1.0f, 1.4f)
                );
                soul.noGravity = true;
                soul.velocity *= 0.3f;
            }

            //暗影焰粒子
            if (Main.rand.NextBool(4)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    4, 4,
                    DustID.Shadowflame,
                    0, 0,
                    150,
                    default,
                    Main.rand.NextFloat(0.8f, 1.1f)
                );
                shadow.noGravity = true;
                shadow.velocity = -Projectile.velocity * 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HasHitEnemy = 1;

            //索魂一击：低血量敌人有几率被索命
            if (target.life < target.lifeMax * 0.15f && !target.boss && Main.rand.NextBool(5)) {
                //对非Boss低血量敌人造成致命伤害
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);

                //产生索魂爆发特效 (保留少量 Dust)
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                    Dust soul = Dust.NewDustPerfect(
                        target.Center,
                        DustID.Wraith,
                        vel,
                        100,
                        default,
                        Main.rand.NextFloat(1.5f, 2.0f)
                    );
                    soul.noGravity = true;
                }

                //处决: 灵魂溶解 + 敌群索魂链
                SoulseekerSoulChain.SpawnExecute(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.3f }, target.Center);
            }

            //击中爆发粒子 (减量)
            for (int i = 0; i < 4; i++) {
                Dust burst = Dust.NewDustDirect(
                    target.Center - Vector2.One * 10,
                    20, 20,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-3f, 3f),
                    Main.rand.NextFloat(-3f, 3f),
                    100,
                    default,
                    1.3f
                );
                burst.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.7f, owner: Projectile.owner);

            //击中音效
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰到方块时产生粒子并消失
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f }, Projectile.position);

            //产生碎裂粒子
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Wraith, oldVelocity.X * 0.2f, oldVelocity.Y * 0.2f, 100, default, 1.0f);
            }

            return true; //消失
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            //幽蓝匕光双层 ribbon 拖尾 (外宽暗紫 + 内窄幽蓝, 流动 UV)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
                outerColor: new Color(80, 50, 120, 140), innerColor: new Color(120, 200, 240, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            //绘制主体
            Color mainColor = Color.Lerp(lightColor, new Color(180, 200, 230), 0.3f);
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

            //匕尖幽蓝柔光 (廉价, 不占名额)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f, new Color(100, 160, 210));

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消失时产生幽魂爆发
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Wraith,
                    Main.rand.NextFloat(-3f, 3f),
                    Main.rand.NextFloat(-3f, 3f),
                    100,
                    default,
                    Main.rand.NextFloat(1.0f, 1.4f)
                );
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 索魂链处决演出 - 纯视觉：命中点灵魂用 DissolveBurn 溶解, 同时向附近敌群拉出 BeamGrad 幽蓝索魂链。
    /// </summary>
    public class SoulseekerSoulChain : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 28;
        private const float ChainRange = 360f;

        /// <summary>处决时生成 (仅 owner 客户端)。</summary>
        public static void SpawnExecute(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulseekerSoulChain>(), 0, 0f, owner);
        }

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

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.3f, 0.45f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            float fade = 1f - life;

            //索魂链: 向附近敌群拉出幽蓝光束
            var targets = new List<Vector2>();
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly)
                    continue;
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) < ChainRange * ChainRange) {
                    targets.Add(npc.Center);
                    if (targets.Count >= 4)
                        break;
                }
            }
            foreach (Vector2 t in targets) {
                ACMShaders.DrawBeam(Projectile.Center, t, halfWidth: 4.5f,
                    core: new Color(120, 210, 245, 200), edge: new Color(50, 50, 140, 0), intensity: fade,
                    flowSpeed: 2.4f, flowScale: 2.6f, coreSharp: 2.6f);
            }

            //命中点灵魂溶解 (DissolveBurn 喂 SoftGlow)
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                WeaponVFX.ApplyDissolveBurn(glow, Projectile.Center, null,
                    new Color(150, 220, 245), 0f, glow.Size() * 0.5f, 1.4f + life * 0.8f,
                    threshold: life, intensity: fade, edgeColor: new Color(120, 240, 255, 220),
                    edgeWidth: 0.1f, noiseScale: 2.4f);
            }

            //初爆径向泛光
            if (life < 0.45f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.06f, fade * 0.7f, new Color(140, 220, 245), 6f);

            return false;
        }
    }
}

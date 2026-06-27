using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 判官笔 - 地府判官用于判定生死的神笔，魔法武器
    /// 肉后初期，发射 3 发自绘"朱批符印"追踪弹 <see cref="JudgmentRuneBolt"/> (取代原版亡魂弹幕)。
    /// 伤害/散射/笔尖发射行为与原版等价, 仅把弹体升级为程序化朱红符文。
    /// </summary>
    public class BrushofJudgment : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 48; //基础伤害
            Item.crit = 4; //暴击率
            Item.DamageType = DamageClass.Magic; //魔法伤害类型
            Item.mana = 8; //魔力消耗
            Item.width = 36; //物品宽度
            Item.height = 36; //物品高度
            Item.useTime = 22; //使用时间
            Item.useAnimation = 22; //使用动画时间
            Item.useStyle = ItemUseStyleID.Shoot; //射击风格
            Item.knockBack = 3f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item8; //魔法使用声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ModContent.ProjectileType<JudgmentRuneBolt>(); //自绘朱批符印弹
            Item.shootSpeed = 12f; //弹幕速度
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //发射3发散射弹幕 (与原版等价)
            int numberProjectiles = 3;
            for (int i = 0; i < numberProjectiles; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return false; //不使用默认发射
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //从笔尖位置发射
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 20f;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 朱批符印弹 - 自绘程序化朱红符文追踪弹 (取代原版 LostSoulFriendly)。
    /// 弹体无 PNG: 旋转的朱批符印 (ACMAsset.Sparkle 符纹 + BlankStar 芯) + RadialBloom 核 + ribbon 朱红拖尾。
    /// </summary>
    public class JudgmentRuneBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.light = 0.4f;
        }

        public override void AI() {
            //符印自转
            Projectile.rotation += 0.18f;

            //温和追踪最近敌人
            NPC target = FindTarget(620f);
            if (target != null) {
                float speed = Projectile.velocity.Length();
                if (speed < 1f)
                    speed = 9f;
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 dir = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), toTarget, 0.07f);
                Projectile.velocity = dir.SafeNormalize(Vector2.UnitX) * speed;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.1f, 0.08f);
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    Main.rand.NextVector2Circular(1.2f, 1.2f), 120, default, 0.9f);
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
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 0.7f, owner: Projectile.owner);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.RedTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //朱红双层 ribbon 拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
                outerColor: new Color(150, 20, 20, 140), innerColor: new Color(255, 90, 70, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 6f);

            //符纹环 (Sparkle = 爆炸线条, 当作朱批符印, 双向自转)
            Texture2D rune = ACMAsset.Sparkle;
            if (rune != null) {
                Color runeColor = new Color(230, 40, 36, 0);
                Main.spriteBatch.Draw(rune, pos, null, runeColor * 0.9f, Projectile.rotation,
                    rune.Size() * 0.5f, 0.26f * pulse, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(rune, pos, null, new Color(255, 120, 90, 0) * 0.7f, -Projectile.rotation * 0.6f,
                    rune.Size() * 0.5f, 0.18f * pulse, SpriteEffects.None, 0f);
            }

            //朱红芯
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Main.spriteBatch.Draw(star, pos, null, new Color(255, 80, 70, 0), Projectile.rotation * 0.5f,
                    star.Size() * 0.5f, 0.22f * pulse, SpriteEffects.None, 0f);
            }

            //核心径向泛光 (走全屏名额, 多弹时自动退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.035f, 0.5f, new Color(230, 50, 45), 5f);
            return false;
        }
    }
}

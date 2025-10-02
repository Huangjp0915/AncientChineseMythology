using AncientChineseMythology.Players;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons
{
    internal class CoffinNail : ModItem
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/CoffinNail";

        public override void SetDefaults()
        {
            //基础属性设置
            Item.damage = 688; //高伤害，符合棺材钉的威力
            Item.DamageType = DamageClass.Melee; //远程武器类型
            Item.width = 34;
            Item.height = 34;
            Item.useTime = 25; //稍慢的使用速度，增加重量感
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing; //投掷动作
            Item.knockBack = 8f; //高击退力
            Item.value = Item.buyPrice(0, 50, 0, 0);
            Item.rare = ItemRarityID.Red; //红色稀有度，符合主题
            Item.UseSound = SoundID.Item1; //临时音效，稍后会自定义
            Item.autoReuse = true;
            Item.noMelee = true; //不使用近战碰撞
            Item.noUseGraphic = true; //隐藏使用图形，因为我们用投射物

            //投射物设置
            Item.shoot = ModContent.ProjectileType<CoffinNailProjectile>();
            Item.shootSpeed = 16f; //快速飞行
            Item.consumable = false; //不消耗，可以无限使用
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            //计算投掷方向
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            
            //添加一些随机偏移，让投掷更真实
            direction = direction.RotatedByRandom(MathHelper.ToRadians(2f));
            
            //创建棺材钉投射物
            var projectile = Projectile.NewProjectileDirect(
                source,
                player.Center + direction * 40f, //从玩家前方发射
                direction * Item.shootSpeed,
                type,
                damage,
                knockback,
                player.whoAmI
            );

            //播放投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with
            {
                Volume = 0.8f,
                Pitch = Main.rand.NextFloat(-0.2f, 0.2f)
            }, player.Center);

            return false; //阻止默认投射物生成
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.IronBar, 15); //铁锭
            recipe.AddIngredient(ItemID.Bone, 10); //骨头
            recipe.AddIngredient(ItemID.SoulofNight, 5); //夜明之魂，增加诡异感
            recipe.AddIngredient(ItemID.Ectoplasm, 3); //灵气，增加灵异属性
            recipe.AddTile(TileID.MythrilAnvil); //秘银砧
            recipe.Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            //添加自定义描述
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc1", "[c/8B0000:来自神秘世界的诡异武器]"));
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc2", "[c/DC143C:投掷时会产生恐怖的红色轨迹]"));
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc3", "[c/B22222:击中敌人后会爆发出诡异的血色能量]"));
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc4", "[c/696969:'就算是鬼，也要被钉死在这里' - 杨间]"));
        }

        public override Color? GetAlpha(Color lightColor)
        {
            //让物品始终保持可见，带有微弱的红色光芒
            return Color.Lerp(lightColor, Color.DarkRed, 0.3f);
        }
    }

    ///<summary>
    ///棺材钉投射物 - 具有华丽的红色轨迹和强大的演出效果
    ///</summary>
    public class CoffinNailProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/CoffinNail";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;//轨迹长度
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;//轨迹模式
        }
        //AI状态变量
        private bool hasHitTarget = false;
        private int trailCounter = 0;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300; //5秒存在时间
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 3; //增加更新频率，让运动更流畅
        }

        public override void AI() {
            //旋转棺材钉
            Projectile.rotation = Projectile.velocity.ToRotation();

            //重力效果（轻微）
            if (Projectile.velocity.Y < 16f && Projectile.numHits == 0) {
                Projectile.velocity.Y += 0.2f;
            }

            //生成红色粒子轨迹
            if (Main.rand.NextBool(2)) {
                CreateBloodParticles();
            }

            //创建恐怖氛围的红色闪烁
            Lighting.AddLight(Projectile.Center, 0.8f, 0.1f, 0.1f);

            //高级运动算法：磁性追踪效果
            if (Projectile.timeLeft > 50) {
                ApplyHomingEffect();
            }
        }

        ///<summary>
        ///创建血色粒子效果
        ///</summary>
        private void CreateBloodParticles() {
            Vector2 dustPosition = Projectile.Center + Main.rand.NextVector2Circular(16, 16);

            Dust bloodDust = Dust.NewDustDirect(
                dustPosition,
                4, 4,
                DustID.Blood,
                Projectile.velocity.X * 0.3f,
                Projectile.velocity.Y * 0.3f,
                100,
                Color.DarkRed,
                Main.rand.NextFloat(1.2f, 2.0f)
            );

            bloodDust.noGravity = true;
            bloodDust.fadeIn = 1.2f;

            //额外的红色火焰效果
            if (Main.rand.NextBool(3)) {
                Dust flameDust = Dust.NewDustDirect(
                    dustPosition,
                    4, 4,
                    DustID.Torch,
                    0, 0, 100,
                    Color.Crimson,
                    Main.rand.NextFloat(0.8f, 1.5f)
                );
                flameDust.noGravity = true;
            }
        }

        ///<summary>
        ///智能追踪效果
        ///</summary>
        private void ApplyHomingEffect() {
            if (Projectile.numHits != 0) {
                Projectile.velocity *= 0.58f;
                return;
            }

            float homingRange = 1200f;
            float homingStrength = 10.5f;

            NPC closestNPC = null;
            float closestDistance = homingRange;

            //寻找最近的敌人
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestNPC = npc;
                    }
                }
            }

            //追踪最近的敌人
            if (closestNPC != null) {
                Vector2 targetDirection = Vector2.Normalize(closestNPC.Center - Projectile.Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDirection * Projectile.velocity.Length(), homingStrength * 0.02f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hasHitTarget = true;

            //播放击中音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.7f,
                Pitch = Main.rand.NextFloat(-0.2f, 0.2f)
            }, target.Center);

            //创建血色爆炸效果
            CreateBloodExplosion(target.Center);

            //对目标施加恐惧效果
            target.AddBuff(BuffID.Confused, 180); //3秒混乱
            target.AddBuff(BuffID.OnFire, 300); //5秒燃烧，增加诡异感
        }

        ///<summary>
        ///创建血色爆炸效果
        ///</summary>
        private void CreateBloodExplosion(Vector2 position) {
            //大量血色粒子
            for (int i = 0; i < 30; i++) {
                Vector2 speed = Main.rand.NextVector2Circular(8f, 8f);
                Dust explosion = Dust.NewDustPerfect(
                    position,
                    DustID.Blood,
                    speed,
                    0,
                    Color.DarkRed,
                    Main.rand.NextFloat(1.5f, 3f)
                );
                explosion.noGravity = true;
                explosion.fadeIn = 1.5f;
            }

            //红色火焰环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

                Dust flame = Dust.NewDustPerfect(
                    position + direction * 20f,
                    DustID.Torch,
                    direction * 5f,
                    0,
                    Color.Crimson,
                    2f
                );
                flame.noGravity = true;
            }

            //创建更多爆炸粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust spark = Dust.NewDustDirect(
                    position - Vector2.One * 8,
                    16, 16,
                    DustID.Flare,
                    vel.X, vel.Y,
                    100, Color.Red, 1.5f
                );
                spark.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞瓦片时的反弹和特效
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);

            //播放碰撞音效
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            //反弹逻辑
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
                Projectile.velocity.X = -oldVelocity.X * 0.7f;

            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;

            return false; //不销毁投射物，让它弹跳
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制红色轨迹
            DrawBloodTrail();

            //绘制主体（带红色光晕）
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Color drawColor = Color.Lerp(lightColor, Color.Red, 0.5f);
            float rotation = Projectile.rotation + MathHelper.ToRadians(-50);

            //主体绘制
            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                null,
                drawColor,
                rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //额外的红色光晕
            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                null,
                Color.Red * 0.3f,
                rotation,
                texture.Size() / 2f,
                Projectile.scale * 1.2f,
                SpriteEffects.None,
                0
            );

            return false; //阻止默认绘制
        }

        ///<summary>
        ///绘制血色轨迹
        ///</summary>
        private void DrawBloodTrail() {
            float sengs = 0.5f;
            //使用简化的轨迹绘制
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }

                float progress = (float)i / Projectile.oldPos.Length;
                float opacity = (1f - progress) * sengs;
                Color trailColor = Color.Lerp(Color.DarkRed, Color.Red, 1f - progress) * opacity;

                Vector2 position = Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2;
                float scale = (1f - progress) * 1;

                //绘制轨迹点
                Texture2D pixel = TextureAssets.Projectile[Type].Value;
                Main.EntitySpriteDraw(
                    pixel,
                    position,
                    null,
                    trailColor,
                    Projectile.rotation + MathHelper.ToRadians(-50),
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0
                );

                sengs *= 0.9f;
            }
        }

        public override void OnKill(int timeLeft) {
            //销毁时的特效
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Blood,
                    Main.rand.NextFloat(-5f, 5f),
                    Main.rand.NextFloat(-5f, 5f),
                    100,
                    Color.DarkRed,
                    Main.rand.NextFloat(1f, 2f)
                );
                death.noGravity = true;
            }

            //播放消失音效
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
        }

        ///<summary>
        ///创建屏幕震动效果
        ///</summary>
        private void CreateScreenShake() {
            //通过ACMPlayer实现屏幕震动
            if (Main.LocalPlayer.active) {
                Main.LocalPlayer.GetModPlayer<ACMPlayer>().ScreenShake(8f, 15);
            }
        }
    }
}

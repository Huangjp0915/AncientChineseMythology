using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses.Items
{
    /// <summary>
    /// 枉死千骸连弩 - 继承Boss的骨头泼洒攻击
    /// </summary>
    internal class CorpsesesRepeater : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 4288;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 64;
            Item.height = 32;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 8;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // 转换为骨箭
            type = ModContent.ProjectileType<CorpsesesBoneArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 每次射击发射3根骨箭，呈扇形散开（模拟泼洒效果）
            int arrowCount = 3;
            float spreadAngle = 0.3f;

            for (int i = 0; i < arrowCount; i++) {
                float angleOffset = MathHelper.Lerp(-spreadAngle, spreadAngle, i / (float)(arrowCount - 1));
                Vector2 perturbedVelocity = velocity.RotatedBy(angleOffset);

                Projectile.NewProjectile(source, position, perturbedVelocity, type, damage, knockback, player.whoAmI);
            }

            return false;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-8f, 0f);
        }

        public override void AddRecipes() {
            // TODO: 添加合成配方
        }
    }

    /// <summary>
    /// 骨箭弹幕 - 受重力影响，类似Boss的骨头泼洒
    /// </summary>
    public class CorpsesesBoneArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneArrow;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.arrow = true;
        }

        public override void AI() {
            // 受重力影响
            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            // 旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 暗影粒子
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.2f;
            }

            // 轻微追踪
            if (Projectile.ai[0] < 30f) {
                Projectile.ai[0]++;
            }
            else {
                NPC target = FindClosestEnemy(Projectile.Center, 300f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                        toTarget * Projectile.velocity.Length(), 0.02f);
                }
            }
        }

        private NPC FindClosestEnemy(Vector2 position, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(npc.Center, position);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            // 爆发粒子
            for (int i = 0; i < 8; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                Main.dust[dust].noGravity = true;
            }

            // 30%概率分裂成小骨头碎片
            if (Main.rand.NextBool(3) && Main.myPlayer == Projectile.owner) {
                for (int i = 0; i < 3; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(6, 6);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                        ModContent.ProjectileType<CorpsesesBoneShard>(), Projectile.damage / 2,
                        Projectile.knockBack * 0.5f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>("Terraria/Images/Projectile_" + ProjectileID.BoneArrow).Value;
            Vector2 origin = texture.Size() / 2f;

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(150, 50, 200) * progress * 0.4f;

                Main.EntitySpriteDraw(texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None);
            }

            // 绘制主体
            Color mainColor = Color.Lerp(lightColor, new Color(150, 50, 200), 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }

    /// <summary>
    /// 骨头碎片 - 分裂弹幕
    /// </summary>
    public class CorpsesesBoneShard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneArrow;
        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.3f;
            Projectile.rotation += 0.3f;

            if (Main.rand.NextBool(5)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}


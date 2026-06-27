using AncientChineseMythology.Helpers;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    public class JiangcenHammerItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";
        public override void SetDefaults() {
            Item.width = 150;
            Item.height = 132;
            Item.damage = 680;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.shootSpeed = 25f;
            Item.knockBack = 6f;
            Item.shoot = ModContent.ProjectileType<JiangcenHammerProj>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Red;
            Item.value = 2000;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }
    }

    public class JiangcenHammerProj : BaseHeldProj
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.localNPCHitCooldown = 30;
            Projectile.extraUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.width = Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 紫色雷电爆发粒子
            for (int i = 0; i < 125; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(16f, 26f);
                Dust d = Dust.NewDustPerfect(
                    target.Center,
                    DustID.PurpleTorch, // 紫色火焰
                    dustVel,
                    150,
                    Color.MediumPurple,
                    Main.rand.NextFloat(11.2f, 31.8f)
                );
                d.noGravity = true;
            }

            // 黑暗雾气
            for (int i = 0; i < 115; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(13f, 33f),
                    200,
                    Color.Purple * 0.7f,
                    Main.rand.NextFloat(11f, 21.5f)
                );
                smoke.noGravity = true;
            }

            // 雷鸣音效
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap, target.Center);

            // 落锤金辉砸击演出: 双环冲击波 + 径向辉光 (ACMWeaponBurst 内置) + 强屏震
            WeaponVFX.AddScreenShake(target.Center, 10f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 1.8f, owner: Projectile.owner);

            base.OnHitNPC(target, hit, damageDone);
        }

        public override void AI() {
            //紫色光效
            Lighting.AddLight(
                Projectile.Center,
                0.5f,
                0.2f,
                0.6f
            );

            Projectile.rotation += 0.4f; // 转得更快

            //拖尾闪光
            Dust trail = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.MagicMirror,
                    -Projectile.velocity * 0.2f,
                    150,
                    Color.MediumPurple,
                    1.2f
                );
            trail.noGravity = true;

            if (Projectile.soundDelay == 0) {
                Projectile.soundDelay = 12;
                SoundEngine.PlaySound(SoundID.Item7, Projectile.position); //魔法雷鸣
            }

            switch (Projectile.ai[0]) {
                case 0f:
                    Projectile.ai[1] += 1f;
                    if (Projectile.ai[1] >= 40f) {
                        Projectile.ai[0] = 1f;
                        Projectile.ai[1] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                case 1f:
                    float returnSpeed = 25f;
                    float acceleration = 5f;
                    Vector2 playerVec = Owner.Center - Projectile.Center;
                    if (playerVec.Length() > 4000f) {
                        Projectile.Kill();
                    }
                    playerVec.Normalize();
                    playerVec *= returnSpeed;

                    //X方向加速
                    if (Projectile.velocity.X < playerVec.X) {
                        Projectile.velocity.X += acceleration;
                        if (Projectile.velocity.X < 0f && playerVec.X > 0f)
                            Projectile.velocity.X += acceleration;
                    }
                    else if (Projectile.velocity.X > playerVec.X) {
                        Projectile.velocity.X -= acceleration;
                        if (Projectile.velocity.X > 0f && playerVec.X < 0f)
                            Projectile.velocity.X -= acceleration;
                    }

                    //Y方向加速
                    if (Projectile.velocity.Y < playerVec.Y) {
                        Projectile.velocity.Y += acceleration;
                        if (Projectile.velocity.Y < 0f && playerVec.Y > 0f)
                            Projectile.velocity.Y += acceleration;
                    }
                    else if (Projectile.velocity.Y > playerVec.Y) {
                        Projectile.velocity.Y -= acceleration;
                        if (Projectile.velocity.Y > 0f && playerVec.Y < 0f)
                            Projectile.velocity.Y -= acceleration;
                    }

                    //回到玩家后消失
                    if (Main.myPlayer == Projectile.owner) {
                        Rectangle projHitbox = new Rectangle((int)Projectile.position.X, (int)Projectile.position.Y, Projectile.width, Projectile.height);
                        Rectangle playerHitbox = new Rectangle((int)Owner.position.X, (int)Owner.position.Y, Owner.width, Owner.height);
                        if (projHitbox.Intersects(playerHitbox)) {
                            Projectile.Kill();
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            float sengs = 0.6f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 drawOldPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.spriteBatch.Draw(mainValue, drawOldPos, rectangle, lightColor * sengs
                    , Projectile.oldRot[i] + MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, rectangle, lightColor
                , Projectile.rotation + MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);

            // 锤头蓄力金辉 (挥砸前 40 帧 ai[0]==0 期间渐亮)
            if (Projectile.ai[0] == 0f) {
                float charge = MathHelper.Clamp(Projectile.ai[1] / 40f, 0f, 1f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, (0.6f + charge * 1.6f) * Projectile.scale,
                    new Color(255, 225, 130) * (0.35f + 0.55f * charge));
            }
            return false;
        }
    }
}

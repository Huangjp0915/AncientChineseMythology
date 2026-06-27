using AncientChineseMythology.Helpers;
using AncientChineseMythology.NPCs.Boss.Yingous;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    public class YingouKnife : ModItem
    {
        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 342;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = 2000;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.shootSpeed = 8f;
            Item.shoot = ModContent.ProjectileType<SaberHellFriendly>();
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            player.itemLocation = player.GetPlayerStabilityCenter();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, Main.MouseWorld, velocity.GetNormalVector(), type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class SaberHellFriendly : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.velocity = Projectile.velocity.UnitVector();
            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) {
                    Projectile.localAI[1] = 30;
                }
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40) {
                    int num = 1000;
                    int num2 = 36;
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Main.projectile[proj].friendly = true;
                    Projectile.velocity *= -1;
                    proj = Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Main.projectile[proj].friendly = true;
                }
            }
            else {
                if (Projectile.localAI[1] > 0) {
                    Projectile.localAI[1]--;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 表现层重做: 用共享 BeamGrad 原语画一条"苍→赤"渐变长刀气, 取代 placeholder 矩形。
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float grow = MathHelper.Clamp(Projectile.localAI[0] / 40f, 0f, 1f);   // 蓄刀生长
            float alpha = MathHelper.Clamp(Projectile.localAI[1] / 30f, 0f, 1f);  // 收刀淡出
            float intensity = grow * (0.4f + 0.6f * alpha);
            if (intensity <= 0.01f)
                return false;

            const float halfLen = 2200f;
            float halfWidth = MathHelper.Clamp(Projectile.localAI[0] * 1.6f, 6f, 110f);
            Color core = Color.Lerp(new Color(150, 220, 255), new Color(255, 60, 50), grow);   // 苍 → 赤
            Color edge = Color.Lerp(new Color(40, 90, 180), new Color(150, 12, 16), grow);

            Vector2 start = Projectile.Center - dir * halfLen;
            Vector2 end = Projectile.Center + dir * halfLen;
            ACMShaders.DrawBeam(start, end, halfWidth, core with { A = 200 }, edge with { A = 120 }, intensity, coreSharp: 2.6f);

            // 刀身锚点的爆闪 (SlashBurst 配方的轻量替代, 不占全屏名额)
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.4f + grow * 2.2f), core * (intensity * 0.9f));
            return false;
        }
    }
}

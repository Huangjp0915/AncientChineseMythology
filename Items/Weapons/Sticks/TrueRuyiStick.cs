using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>
    /// 真·如意棍: 三段连段 + 每 5 挥"双头回环" (棍两端均有判定的大回环 1.8x);
    /// 右键定海神针 (真规格: 蓄力更快, 针大 25%, 落地分两根侧针)。幽蓝×金配色 (四魂所铸)。
    /// 从旧版白板 Swing 占位全面重做。
    /// </summary>
    public class TrueRuyiStick : StickWeaponItem
    {
        // 不新增贴图: 物品图标复用系列既有棍弹幕贴图 (替换旧版 vanilla 占位图)
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        protected override int ComboLength => 3;
        protected override int SpecialEvery => 5;
        protected override int SpecialStepIndex => 3;

        public override void SetDefaults() {
            Item.damage = 240;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.knockBack = 6.5f;
            Item.value = 30000;
            Item.rare = ItemRarityID.LightRed;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<TrueRuyiSwingProj>();
            Item.shootSpeed = 3.5f;
        }

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<RuyiStickSpearProjectile_2>()] > 0)
                return false;
            return base.CanUseItem(player);
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            // ai[0] = 1: 真·规格 (蓄力更快 / 针 +25% / 侧针)
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<RuyiStickSpearProjectile_2>(), damage, knockback, player.whoAmI, 1f);
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<RuyiStick>(), 1);
            recipe.AddIngredient(ItemID.SoulofFright, 15);
            recipe.AddIngredient(ItemID.SoulofMight, 15);
            recipe.AddIngredient(ItemID.SoulofSight, 15);
            recipe.AddIngredient(ItemID.SoulofFlight, 10);
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.Register();
        }
    }

    /// <summary>
    /// 真·如意棍持械弹幕: 横扫 → 回扫 → 重抡 + 每 5 挥双头回环。
    /// 幽蓝×金双色; 棍身走 RuyiGoldenCudgel 着色器低强度档 (辅色传幽蓝)。
    /// </summary>
    internal class TrueRuyiSwingProj : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        public override LocalizedText DisplayName
            => Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RuyiStickSpearProjectile.DisplayName");

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.8f, 1f),
            SwingStep.Sweep(3.8f, 1.08f, sign: -1),
            SwingStep.Sweep(4.6f, 1.3f, sign: 1, timeMul: 1.25f, scaleMul: 1.12f, impact: true),
            SwingStep.Spin(1.5f, 1.8f, timeMul: 1.5f, scaleMul: 1.2f), // 双头回环
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 22;
        protected override Color TrailOuter => new(35, 60, 140, 160);
        protected override Color TrailInner => new(255, 215, 130, 210);
        protected override float TipLength => 110f;
        protected override float Overshoot => 0.2f;
        protected override int BurstTheme => ACMWeaponBurst.Fatal;
        protected override float HitShake => 2.2f;
        protected override int HitDustType => DustID.GoldFlame;
        protected override Vector3 GlowLight => new(0.35f, 0.35f, 0.6f);

        protected override void OnStrikeStart(SwingStep step) {
            if (StepIndex == 3) {
                // 双头回环: 双层重音 + 起手震
                Terraria.Audio.SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1f, Pitch = -0.2f }, Projectile.Center);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.1f }, Projectile.Center);
                Helpers.WeaponVFX.AddScreenShake(Owner.Center, 2f);
                return;
            }
            base.OnStrikeStart(step);
        }

        protected override void DrawStick(Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            GetDrawParams(tex, out Vector2 origin, out float rotOff, out SpriteEffects fx);
            float flash = MathHelper.Clamp((LengthPulse - 1f) / Overshoot, 0f, 1f) * 0.3f;
            RuyiCudgelVFX.DrawStickWithShader(tex, StickDrawCenter() - Main.screenPosition,
                Projectile.rotation + rotOff, origin, Projectile.scale * LengthPulse, fx,
                lightColor * Projectile.Opacity,
                intensity: 0.45f, charge: 0.45f, flash: flash,
                gold: new Color(255, 215, 130), secondary: new Color(90, 130, 255));
        }
    }
}

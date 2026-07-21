using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>
    /// 棍系物品共享基类: 连段推进 (2s 未使用重置) + 特殊段计数 (每 N 次触发大段) + 右键分支。
    /// 全部状态存于物品实例 (owner 端 Shoot 消费, 段序号经 ai[0] 随生成包同步)。
    /// </summary>
    public abstract class StickWeaponItem : ModItem
    {
        protected int comboStep;
        protected int comboExpire;
        protected int specialCounter;
        protected int altCooldown;

        /// <summary>左键连段长度。</summary>
        protected virtual int ComboLength => 2;
        /// <summary>每挥 N 次后下一击变特殊段 (0 = 无特殊段)。</summary>
        protected virtual int SpecialEvery => 0;
        /// <summary>特殊段在 Steps 表中的序号。</summary>
        protected virtual int SpecialStepIndex => -1;
        /// <summary>右键冷却帧 (0 = 无冷却)。</summary>
        protected virtual int AltCooldownFrames => 0;

        public override Color? GetAlpha(Color lightColor) => Color.White;

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void UpdateInventory(Player player) {
            if (comboExpire < 600 && ++comboExpire >= 120) {
                comboStep = 0;
                specialCounter = 0;
            }
            if (altCooldown > 0)
                altCooldown--;
        }

        /// <summary>取下一个连段序号并推进计数。</summary>
        protected int NextStep() {
            comboExpire = 0;
            if (SpecialEvery > 0 && specialCounter >= SpecialEvery) {
                specialCounter = 0;
                return SpecialStepIndex;
            }
            int step = comboStep;
            comboStep = (comboStep + 1) % ComboLength;
            specialCounter++;
            return step;
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2 && altCooldown > 0)
                return false;
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                altCooldown = AltCooldownFrames;
                ShootAlt(player, source, position, velocity, damage, knockback);
            }
            else {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, NextStep());
            }
            return false;
        }

        /// <summary>右键分支 (默认无)。</summary>
        protected virtual void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) { }
    }

    /// <summary>木棍: 两段连段; 右键"如意戳"。</summary>
    public class WoodenStick : StickWeaponItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/WoodenStick";

        protected override int ComboLength => 2;

        public override void SetDefaults() {
            Item.damage = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(silver: 0);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<WoodenStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            // 击退加成由弹幕 ModifyHitNPC 提供 (×1.5), 此处传原值
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<WoodenStickSpearProjectile_2>(), damage, knockback, player.whoAmI);
        }
    }
}

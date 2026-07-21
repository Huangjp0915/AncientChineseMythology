using AncientChineseMythology.Buffs;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    /// <summary>
    /// 黑熊法杖: 召唤一只黑熊幼灵 (独宠, 上限 1 只, 0.5 召唤栏)。
    /// 幼灵以 蓄势→黑风猛扑→落掌震击 循环作战, 每第 4 掌为金冠怒击。
    /// </summary>
    public class BlackBearStaff : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/BlackBearStaff";

        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 0.5f;
        }

        public override void SetDefaults() {
            Item.damage = 25;
            Item.crit = 3;
            Item.DamageType = DamageClass.Summon;
            Item.width = 40;
            Item.height = 28;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 30, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.mana = 10;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<BlackBearStaffProj1>();
            Item.shootSpeed = 1f;
            Item.buffType = ModContent.BuffType<BuffsBlackBearStaff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 独宠上限: 已有幼灵则只刷新 Buff (不重复召唤)
            player.AddBuff(Item.buffType, 3);
            if (player.ownedProjectileCounts[type] >= 1)
                return false;

            // 召唤仪式: 鼠标点显形 (Shoot 天然只在 owner 客户端执行)
            var projectile = Projectile.NewProjectileDirect(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = -0.1f }, Main.MouseWorld);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                Dust d = Dust.NewDustPerfect(Main.MouseWorld, DustID.Smoke, vel, 140, new Color(50, 48, 66), 1.3f);
                d.noGravity = true;
                if (i % 3 == 0) {
                    Dust g = Dust.NewDustPerfect(Main.MouseWorld, DustID.GoldCoin, vel * 0.7f, 100, default, 0.9f);
                    g.noGravity = true;
                }
            }
            return false;
        }
    }
}

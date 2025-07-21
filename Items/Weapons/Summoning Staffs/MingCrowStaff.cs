using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    public class MingCrowStaff : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/MingCrowStaff";
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults() {
            Item.damage = 12;
            Item.DamageType = DamageClass.Summon;
            Item.mana = 10;
            Item.width = 42;
            Item.height = 42;
            Item.useTime = Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.value = Item.sellPrice(gold: 80);
            Item.rare = ItemRarityID.Orange;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;

            Item.buffType = ModContent.BuffType<MingCrowMinionBuff>();
            Item.shoot = ModContent.ProjectileType<Projectiles.Minions.MingCrowMinion>();
            Item.shootSpeed = 10f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
                                   Vector2 position, Vector2 velocity,
                                   int type, int damage, float knockback) {
            // ① 生成新冥鸦（引擎会自动处理“槽已满”→牺牲旧召唤物）
            position = Main.MouseWorld;
            velocity = Vector2.Zero; // 初速 0，AI 自行加速

            int proj = Projectile.NewProjectile(source, position, velocity,
                                                type, damage, knockback, player.whoAmI);
            if (proj >= 0)
                Main.projectile[proj].originalDamage = Item.damage;

            // ② 维持 Buff（2 帧，Buff 自身脚本会持续刷新至常驻）
            player.AddBuff(Item.buffType, 2);

            // 返回 false 告诉 tML：我们已手动生成 Projectile
            return false;
        }
    }
}

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
    /// 冥鸦法杖: 召唤阴间鸦群为你作战 (每只占 1 召唤栏, 可多只)。
    /// 鸦群错拍环伺 → 俯冲穿透, 每第 3 次为缠绕俯冲 (×1.4 + 鸦羽爆散)。
    /// </summary>
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
            Item.shoot = ModContent.ProjectileType<MingCrowMinion>();
            Item.shootSpeed = 10f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
                                   Vector2 position, Vector2 velocity,
                                   int type, int damage, float knockback) {
            // 在鼠标点召唤 (槽满时引擎自动牺牲旧召唤物); Shoot 天然只在 owner 客户端执行
            position = Main.MouseWorld;

            int proj = Projectile.NewProjectile(source, position, Vector2.Zero,
                                                type, damage, knockback, player.whoAmI);
            if (proj >= 0)
                Main.projectile[proj].originalDamage = Item.damage;

            player.AddBuff(Item.buffType, 2);

            // 召唤起手音 (低频铺垫叠在 Item44 上)
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = -0.4f }, position);
            return false;
        }
    }
}

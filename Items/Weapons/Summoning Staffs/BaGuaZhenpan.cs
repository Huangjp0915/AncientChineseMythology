using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Buffs;
using AncientChineseMythology.Projectiles;
using AncientChineseMythology.UI;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    public class BaGuaZhenpan : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/BaGuaZhenpan";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(0, 5);
        }

        // 启用右键逻辑
        public override bool AltFunctionUse(Player player) => true;

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2) // 右键：切换 UI
            {
                BaGuaUISystem.Toggle(player);   // static 方法开关
            }
            else                          // 左键：施加 Buff + 生成阵图
            {
                const int buffTime = 60 * 60; // 60 秒
                player.AddBuff(ModContent.BuffType<BaGuaBuff>(), buffTime);

                // 确保同一玩家只有一个阵图
                if (player.ownedProjectileCounts[ModContent.ProjectileType<BaGuaSigilProj>()] == 0)
                {
                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        default,
                        ModContent.ProjectileType<BaGuaSigilProj>(),
                        0, 0f, player.whoAmI);
                }
            }
            return true;
        }
    }
}

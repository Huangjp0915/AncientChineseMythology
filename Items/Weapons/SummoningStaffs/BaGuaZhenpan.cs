using AncientChineseMythology.Buffs;
using AncientChineseMythology.Projectiles;
using AncientChineseMythology.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    /// <summary>
    /// 八卦阵盘: 左键开坛施阵 (BaGuaBuff + 场上八卦法阵), 右键打开布阵 UI。
    /// 阵法效果本体在 Players/BaGuaPlayer.cs (8 槽布阵系统), 本物品只负责入口与仪式演出。
    /// </summary>
    public class BaGuaZhenpan : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/BaGuaZhenpan";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(5, 0, 0, 0);
        }

        //启用右键逻辑
        public override bool AltFunctionUse(Player player) => true;

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 右键: 布阵 UI 是纯本地界面, 只在本地玩家客户端开关
                if (player.whoAmI == Main.myPlayer) {
                    BaGuaUISystem.Toggle(player);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            else {
                // 左键: 开坛施阵 (BaGuaBuff 自刷新, 直到玩家手动取消)
                player.AddBuff(ModContent.BuffType<BaGuaBuff>(), 60 * 60);

                // 法阵弹幕仅 owner 客户端生成 (NewProjectile 自动同步), 修复多端重复生成
                if (player.whoAmI == Main.myPlayer
                    && player.ownedProjectileCounts[ModContent.ProjectileType<BaGuaSigilProj>()] == 0) {
                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        default,
                        ModContent.ProjectileType<BaGuaSigilProj>(),
                        0, 0f, player.whoAmI);
                }

                // 开坛起手音 (低频铺垫; 逐卦点亮的音阶由法阵弹幕自己演奏)
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.35f }, player.Center);
            }
            return true;
        }
    }
}

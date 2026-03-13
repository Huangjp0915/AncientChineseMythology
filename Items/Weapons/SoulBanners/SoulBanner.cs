using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    public class SoulBanner : ModItem
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.damage = 52;
            Item.DamageType = DamageClass.Summon;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightPurple;
            Item.mana = 15;
            Item.autoReuse = true;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulBannerHeldProj>();
            Item.shootSpeed = 3.5f;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                Item.useTime = 36;
                Item.useAnimation = 36;
                Item.mana = 30;

                int minionType = ModContent.ProjectileType<SoulBannerMinion>();
                if (player.ownedProjectileCounts[minionType] >= 1)
                    return false;
            }
            else
            {
                Item.useTime = 30;
                Item.useAnimation = 30;
                Item.mana = 15;
            }

            return base.CanUseItem(player);
        }

        /// <summary>成长系统：根据灵魂数量动态修改武器伤害</summary>
        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            var sbPlayer = player.GetModPlayer<SoulBannerPlayer>();
            damage *= sbPlayer.DamageMultiplier;
        }

        /// <summary>成长系统：根据灵魂数量动态修改击退</summary>
        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback)
        {
            var sbPlayer = player.GetModPlayer<SoulBannerPlayer>();
            knockback *= sbPlayer.KnockbackMultiplier;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                player.AddBuff(ModContent.BuffType<SoulBannerMinionBuff>(), 2);
                var proj = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<SoulBannerMinion>(), damage, knockback, player.whoAmI);
                proj.originalDamage = Item.damage;
            }
            else
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<SoulBannerHeldProj>(), damage, knockback, player.whoAmI);
            }

            return false;
        }

        /// <summary>成长系统：动态 Tooltip 显示灵魂数量和成长加成</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;
            var sbPlayer = player.GetModPlayer<SoulBannerPlayer>();

            if (sbPlayer.soulCap > 0)
            {
                float ratio = sbPlayer.GrowthRatio;
                int pct = (int)(ratio * 100);
                string soulLine = $"[c/B455FF:灵魂：{sbPlayer.soulCount} / {sbPlayer.soulCap}  ({pct}%)]";
                tooltips.Add(new TooltipLine(Mod, "SoulCount", soulLine));

                if (sbPlayer.soulCount > 0)
                {
                    int dmgPct = (int)((sbPlayer.DamageMultiplier - 1f) * 100);
                    int radiusPct = (int)((sbPlayer.AbsorbRadiusMultiplier - 1f) * 100);
                    int healPct = (int)((sbPlayer.HealMultiplier - 1f) * 100);
                    string bonusLine = $"[c/9B59B6:伤害+{dmgPct}%  吸魂范围+{radiusPct}%  回复+{healPct}%]";
                    tooltips.Add(new TooltipLine(Mod, "SoulBonus", bonusLine));
                }
            }
            else
            {
                tooltips.Add(new TooltipLine(Mod, "SoulHint", "[c/666666:击败更强大的妖魔以唤醒幡中亡魂]"));
            }

            // 显示下一个 Boss 提示
            string nextBoss = GetNextBossHint(sbPlayer);
            if (nextBoss != null)
                tooltips.Add(new TooltipLine(Mod, "SoulNextBoss", $"[c/555555:{nextBoss}]"));
        }

        private static string GetNextBossHint(SoulBannerPlayer sbPlayer)
        {
            foreach (var tier in SoulBannerPlayer.Tiers)
            {
                if (!sbPlayer.defeatedBossTiers.Contains(tier.TierId))
                    return $"击败下一位强敌以解锁灵魂上限 → {tier.CapValue}";
            }
            if (sbPlayer.soulCount < sbPlayer.soulCap)
                return "继续吸收灵魂以达到上限";
            return null;
        }
    }
}

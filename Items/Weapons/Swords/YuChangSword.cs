using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 鱼肠剑 — 专诸刺王僚, 鱼腹藏锋。刺客短剑重做:
    /// 左键高速突刺 (crit 80 身份保留), 突刺曲线改"快出慢收"; 每第 4 刺"透骨刺"——
    /// 伤害 ×1.5 + 放出鱼影飞刃 + 向光标短滑步, 寒银白闪。
    /// 右键"穿心·背刺"(8s 冷却): 有目标 → 瞬身至其背侧三段连刺, 残血非 Boss 直接处决;
    /// 无目标 → 幽影突进单段重刺。配色统一寒银白青, 处决一抹致命红。
    /// </summary>
    public class YuChangSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/YuChangSword";

        private const int PierceEvery = 4;          // 每第 N 刺透骨
        private const int SkillCooldown = 8 * 60;   // 穿心·背刺冷却 (原 20s → 8s, 倍率同步下调)

        private int attackCounter;
        private int cooldownTimer;

        public override void SetDefaults() {
            Item.damage = 35;
            Item.crit = 80;
            Item.DamageType = DamageClass.Melee;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.knockBack = 4;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.value = Item.buyPrice(0, 100, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<YuChangSwordProjectile>();
            Item.shootSpeed = 1f;
            Item.noMelee = true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                if (cooldownTimer > 0)
                    return false;
                Item.useStyle = ItemUseStyleID.HoldUp;
                Item.useTime = 24;
                Item.useAnimation = 24;
                Item.shoot = ModContent.ProjectileType<YuChangSkillProjectile>();
                Item.shootSpeed = 1f;
                Item.noMelee = true;
                return player.ownedProjectileCounts[ModContent.ProjectileType<YuChangSkillProjectile>()] == 0;
            }

            Item.useStyle = ItemUseStyleID.Rapier;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.shoot = ModContent.ProjectileType<YuChangSwordProjectile>();
            Item.shootSpeed = 1f;
            Item.noMelee = true;
            return player.ownedProjectileCounts[ModContent.ProjectileType<YuChangSwordProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 pos, Vector2 vel, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                ShootBackstab(player, source, damage, knockback);
                return false;
            }

            // —— 左键突刺 ——
            bool pierce = ++attackCounter >= PierceEvery;
            if (pierce)
                attackCounter = 0;

            int thrustDamage = pierce ? (int)(damage * 1.5f) : damage;
            Projectile.NewProjectile(source, pos, vel, type, thrustDamage, knockback, player.whoAmI, 0f, pierce ? 1f : 0f);

            if (pierce) {
                // 透骨刺: 鱼影飞刃 + 向光标短滑步 + 寒银白闪
                Vector2 dir = vel.SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(source, player.Center, dir * 20f,
                    ModContent.ProjectileType<YuChangSwordBeanProjectile>(),
                    damage, knockback, player.whoAmI, ai0: MathHelper.ToRadians(45));
                player.velocity += dir * 6.5f; // owner 端滑步冲量, 位置原生同步
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.55f, Volume = 0.9f }, player.Center);
            }
            return false;
        }

        /// <summary>右键·穿心背刺: 瞬身目标背侧三段连刺 / 无目标幽影突进 (仅 owner 端执行)。</summary>
        private void ShootBackstab(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            cooldownTimer = SkillCooldown;

            NPC target = FindTargetNearCursor(player, 600f);
            Vector2 oldPos = player.Center;

            if (target != null) {
                // 背侧落点 (目标背对面; 落点实心则退回目标另一侧, 再不行原地)
                Vector2 backPos = target.Center + new Vector2(-target.direction * (target.width * 0.5f + 48f), -6f);
                if (SolidAt(player, backPos))
                    backPos = target.Center + (player.Center - target.Center).SafeNormalize(Vector2.UnitX) * (target.width * 0.5f + 48f);
                if (!SolidAt(player, backPos)) {
                    player.Center = backPos;
                    player.fallStart = (int)(player.position.Y / 16f);
                    player.velocity *= 0.2f;
                }
                GhostFlashFX(oldPos, player.Center);
                Projectile.NewProjectile(source, player.Center, (target.Center - player.Center).SafeNormalize(Vector2.UnitX),
                    ModContent.ProjectileType<YuChangSkillProjectile>(), damage, knockback, player.whoAmI, target.whoAmI);
            }
            else {
                // 幽影突进: 向光标强冲量 + 单段重刺 (×2)
                Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                player.velocity = dir * 15f;
                GhostFlashFX(oldPos, player.Center);
                Projectile.NewProjectile(source, player.Center, dir,
                    ModContent.ProjectileType<YuChangSkillProjectile>(), damage * 2, knockback, player.whoAmI, -1f);
            }
        }

        private static NPC FindTargetNearCursor(Player player, float maxDist) {
            NPC best = null;
            float bestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Main.MouseWorld, npc.Center);
                if (d < bestDist && Vector2.Distance(player.Center, npc.Center) < 900f) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        private static bool SolidAt(Player player, Vector2 center) {
            return Collision.SolidCollision(center - new Vector2(player.width * 0.5f, player.height * 0.5f), player.width, player.height);
        }

        /// <summary>出鞘无声: 原地残影寒银雾 + 消音短哨。</summary>
        private static void GhostFlashFX(Vector2 from, Vector2 to) {
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.5f }, to);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 16; i++) {
                Dust d = Dust.NewDustPerfect(from + Main.rand.NextVector2Circular(16f, 24f), DustID.WhiteTorch,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 140, default, Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(to + Main.rand.NextVector2Circular(10f, 18f), DustID.IceTorch,
                    Main.rand.NextVector2Circular(1f, 1f), 160, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        public override void UpdateInventory(Player player) {
            if (cooldownTimer > 0)
                cooldownTimer--;
        }
    }

    public class YuChangSwordFishingPlayer : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            //1% 几率钓到鱼肠剑 (鱼腹藏锋 — 获取途径保留)
            if (Main.rand.NextFloat() < 0.01f) {
                itemDrop = ModContent.ItemType<YuChangSword>();
            }
        }
    }
}

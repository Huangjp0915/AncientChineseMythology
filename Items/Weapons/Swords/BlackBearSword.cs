using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 勇士金剑 (黑熊剑) — 黑风山石中剑, 呼应黑熊精 (墨黑暗风 + 蜂蜜琥珀配色, 取自 Docs/BossRedo/BlackBear.md 色彩语言)。
    /// 重做: 修复 useTime 150 / useAnimation 45 失步 (挥完僵直 1.75s) → 60/60 重剑一秒一挥;
    /// 命中叠"蜜渍"(受伤 +8%, 4s) 建立先涂蜜后重击的决策点;
    /// 每第 3 挥从身后黑风中扑出"黑风熊掌"剑气 (×1.5), 不再从屏幕边缘飞入、不再借用 Boss 头贴图。
    /// </summary>
    public class BlackBearSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BlackBearSword";

        private int swingCounter;

        public override void SetDefaults() {
            Item.damage = 42;                    // 原 47; 失步修复后节奏变快, 论证见 Docs/WeaponRedo/Swords.md §6
            Item.crit = 15;
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 60;                   // 与 useAnimation 同步 (原 150/45 失步为明显失误)
            Item.useAnimation = 60;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 15;
            Item.value = Item.buyPrice(0, 30, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1 with { Pitch = -0.35f, PitchVariance = 0.1f, Volume = 1.1f };
            Item.autoReuse = true;
            Item.noUseGraphic = false;
            Item.shoot = ModContent.ProjectileType<BlackBearSwordProj1>();
            Item.shootSpeed = 16;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            swingCounter++;
            if (swingCounter >= 3) {
                swingCounter = 0;
                // 黑风熊掌: 从玩家身后黑风中扑出 (生成位置/加速由弹幕自理, 方向经 velocity 传递)
                Projectile.NewProjectile(source, player.MountedCenter, velocity,
                    ModContent.ProjectileType<BlackBearSwordProj1>(), (int)(damage * 1.5f), knockback * 0.6f, player.whoAmI);
            }
            return false;
        }

        // 墨黑暗风尘 + 琥珀蜜光点 (纯表现)
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height,
                    DustID.TintableDustLighted, 0f, 0f, 120, new Color(38, 52, 42), Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
                d.velocity *= 0.45f;
            }
            if (Main.rand.NextBool(4)) {
                Dust g = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.GoldFlame);
                g.noGravity = true;
                g.velocity *= 0.3f;
                g.scale = Main.rand.NextFloat(0.7f, 1f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.28f, 0.22f, 0.06f);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage)
                target.AddBuff(ModContent.BuffType<BlackBearHoneyGlazed>(), 60 * 4);
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Bronze, scale: 0.9f, owner: player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
        }
    }

    /// <summary>
    /// 蜜渍 — 黑熊剑命中涂蜜: 受到的伤害 +8% (4s)。琥珀蜜金滴落表现。
    /// </summary>
    public class BlackBearHoneyGlazed : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Buffs.BlackBearHoneyGlazed.DisplayName", () => "蜜渍");
        public override LocalizedText Description => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Buffs.BlackBearHoneyGlazed.Description", () => "被蜂蜜蜜渍, 受到的伤害提高");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }
    }

    /// <summary>蜜渍易伤挂钩: 带蜜渍的 NPC 受伤 +8%; 附带琥珀蜜滴表现。</summary>
    public class BlackBearHoneyGlazedNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (npc.HasBuff(ModContent.BuffType<BlackBearHoneyGlazed>()))
                modifiers.FinalDamage *= 1.08f;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (npc.HasBuff(ModContent.BuffType<BlackBearHoneyGlazed>())) {
                // 琥珀蜜渍着色 + 偶发蜜滴
                drawColor = Color.Lerp(drawColor, new Color(235, 190, 110), 0.22f);
                if (Main.rand.NextBool(14)) {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Honey2);
                    d.velocity = new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f));
                    d.scale = Main.rand.NextFloat(0.8f, 1.2f);
                }
            }
        }
    }
}

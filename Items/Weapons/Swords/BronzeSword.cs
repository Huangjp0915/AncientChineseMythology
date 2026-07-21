using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 青铜剑 — 商周青铜, 剑身淬毒泛绿。
    /// 重做机制"断金": 原 1% 无差别秒杀 (对 Boss 也生效, 不可读) 改为可读的斩杀线处决 —
    /// 命中生命 &lt;12% 的非 Boss 敌人直接断金即杀 (暖金绿爆发), 对 Boss/其余目标改为 +15% 增伤。
    /// 中毒保留。收割残血成为主动决策点。
    /// </summary>
    public class BronzeSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BronzeSword";

        private const float ExecuteThreshold = 0.12f; // 断金斩杀线 (生命比例)

        public override void SetDefaults() {
            Item.damage = 8;
            Item.crit = 15;
            Item.DamageType = DamageClass.Melee;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 0;
            Item.value = Item.buyPrice(0, 0, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1 with { PitchVariance = 0.15f };
            Item.autoReuse = true;
            Item.noUseGraphic = false;
            Item.shoot = ModContent.ProjectileType<BlankProjectile>();
            Item.shootSpeed = 1f;
        }

        // 青铜暖金尘拖尾 + 淬毒绿磷光 (纯表现)
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                int type = Main.rand.NextBool(4) ? DustID.GoldFlame : DustID.Copper;
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, type);
                d.noGravity = true;
                d.velocity *= 0.4f;
                d.scale = Main.rand.NextFloat(0.8f, 1.3f);
            }
            if (Main.rand.NextBool(6)) {
                Dust g = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.GreenTorch);
                g.noGravity = true;
                g.velocity *= 0.25f;
                g.scale = Main.rand.NextFloat(0.6f, 0.9f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.4f, 0.33f, 0.12f);
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            // 断金: 斩杀线之下 → 非 Boss 即杀 (以剩余生命平伤实现, 走正常伤害管线保留掉落/OnKill 钩子); Boss 增伤 15%
            if (target.life > 0 && target.life < target.lifeMax * ExecuteThreshold && !target.friendly && !target.dontTakeDamage) {
                if (!target.boss && target.lifeMax > 5)
                    modifiers.FlatBonusDamage += target.life; // 防御结算后追加剩余生命 → 必杀
                else
                    modifiers.FinalDamage *= 1.15f;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.Poisoned)) {
                target.AddBuff(BuffID.Poisoned, 180);
            }

            // 断金处决演出: 目标被这一击带走 → 暖金绿爆发 + 清脆双层音 + 微震
            if (target.life <= 0) {
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bronze, scale: 1.4f, owner: player.whoAmI);
                WeaponVFX.AddScreenShake(target.Center, 1.5f);
                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.4f, Volume = 0.8f }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.2f, Volume = 0.7f }, target.Center);
            }
            else if (Main.rand.NextBool(2)) {
                // 命中 SoftGlow 闪 (轻量, 隔次触发省开销)
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bronze, scale: 0.7f, owner: player.whoAmI);
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BronzeIngot>(), 18)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

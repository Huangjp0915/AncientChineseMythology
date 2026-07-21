using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 骨剑 — 超快挥速 (useTime 6) 的上古骨器。
    /// 重做机制"碎骨": 命中计数, 每第 8 击伤害 ×2 并触发骨白爆发 + 骨屑喷溅 + 轻屏震 + 低沉音,
    /// 换目标不清零 → 鼓励贴脸连打的节奏高点。其余保持低阶朴素。
    /// </summary>
    public class BoneSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BoneSword";

        private const int ShatterEvery = 8; // 第 N 击触发碎骨
        private int hitCounter;             // 本地手感层计数 (owner 端命中驱动)

        public override void SetDefaults() {
            Item.damage = 3;
            Item.crit = 5;
            Item.DamageType = DamageClass.Melee;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 6;
            Item.useAnimation = 6;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 0;
            Item.value = Item.buyPrice(0, 0, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1 with { PitchVariance = 0.15f, Pitch = 0.1f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<BlankProjectile>();
            Item.shootSpeed = 16;
            Item.noUseGraphic = false;
        }

        // 极廉价骨白尘 (挥速极快, 强力门控避免刷屏); 临近碎骨时骨尘渐密作为"充能广播"
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            int gate = hitCounter >= ShatterEvery - 2 ? 2 : 4;
            if (Main.rand.NextBool(gate)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Bone);
                d.noGravity = true;
                d.velocity *= 0.3f;
                d.scale = Main.rand.NextFloat(0.6f, 0.9f);
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            // 即将落下的这一击是第 8 击 → 碎骨 ×2
            if (hitCounter >= ShatterEvery - 1)
                modifiers.FinalDamage *= 2f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            hitCounter++;
            if (hitCounter >= ShatterEvery) {
                hitCounter = 0;
                // 碎骨节拍: 骨白爆发 + 骨屑喷溅 + 微后坐 + 低沉音
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bone, scale: 1.3f, owner: player.whoAmI);
                WeaponVFX.AddScreenShake(target.Center, 1.5f);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.4f, Volume = 0.9f }, target.Center);
                if (player.whoAmI == Main.myPlayer)
                    player.velocity -= player.DirectionTo(target.Center) * 0.6f; // 反作用微后坐
                if (!Main.dedServ) {
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustPerfect(target.Center, DustID.Bone,
                            Main.rand.NextVector2Circular(4.5f, 4.5f), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                        d.noGravity = Main.rand.NextBool();
                    }
                }
            }
            else if (Main.rand.NextBool(6)) {
                // 平击偶发轻反馈, 保持极低开销
                ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Bone, scale: 0.6f, owner: player.whoAmI);
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Bone>(), 20)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    public class HanbaBook : ModItem
    {
        public override void SetDefaults() {
            Item.useTime = Item.useAnimation = 32;
            Item.mana = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.value = 2000;
            Item.rare = ItemRarityID.Red;
            Item.damage = 145;
            Item.DamageType = DamageClass.Magic;
            Item.UseSound = SoundID.Item74;
            Item.shoot = ModContent.ProjectileType<HanbaBookProj>();
        }
    }

    public class HanbaBookProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hanbas/Shockwave";
        public Dictionary<int, float> DmgSoumd { get; set; } = [];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.ai[0]++;
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.ai[0] * 16, Projectile.ai[0] * 16),
                    DustID.Torch,
                    Main.rand.NextVector2Circular(1f, 1f),
                    150,
                    Color.OrangeRed,
                    1.5f
                );
                d.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            NPC npc = target;
            if (target.realLife >= 0) {
                npc = Main.npc[target.realLife];
            }
            DmgSoumd.TryAdd(npc.type, 1f);
            DmgSoumd[npc.type] *= 0.96f;
            modifiers.FinalDamage *= DmgSoumd[npc.type];
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Projectile.ai[0] * 16, targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 旱魃焦土命中演出 (径向辉光 + 冲击环, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Scorch, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;

            // 颜色渐变：从橙色到红色再到透明
            float progress = Projectile.ai[0] / 30f;
            Color drawColor = Color.Lerp(Color.OrangeRed, Color.Red, progress) * (1f - progress);
            drawColor.A = 0;

            // 呼吸式缩放
            float baseScale = Projectile.ai[0] / 36f;
            float pulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.05f;

            // 主冲击波
            Main.spriteBatch.Draw(
                value,
                Projectile.Center - Main.screenPosition,
                null,
                drawColor,
                0,
                value.Size() / 2,
                baseScale * pulse,
                SpriteEffects.None,
                0
            );

            // 残影层（更淡更大）
            Main.spriteBatch.Draw(
                value,
                Projectile.Center - Main.screenPosition,
                null,
                drawColor * 0.5f,
                0,
                value.Size() / 2,
                baseScale * 1.2f,
                SpriteEffects.None,
                0
            );

            // 焦土橙焰脉动 (廉价柔光, 不占全屏名额; 代偿 GenericWarp 热浪)
            float warm = 0.4f + Projectile.ai[0] / 30f * 1.4f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, warm, new Color(255, 140, 45) * (0.7f * (1f - progress)));
            return false;
        }
    }
}

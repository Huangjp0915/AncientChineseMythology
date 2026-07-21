using AncientChineseMythology.Buffs;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Mounts;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    /// <summary>
    /// 承影剑 (御剑坐骑召唤): 《列子·汤问》"昧爽之交……淡淡焉若有物存, 莫识其状"。
    /// 使用时上剑 (ChengYingMount), 并播放一段影剑显形仪式 (ChengYingSummonGlint)。
    /// 骑乘时的影剑语言与冲撞判定见 Projectiles/ChengYingHitbox.cs。
    /// </summary>
    public class ChengYingReins : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/ChengYing";

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 20;
            Item.UseSound = SoundID.Item79;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.noMelee = true;
            Item.mountType = ModContent.MountType<ChengYingMount>();
        }

        public override bool? UseItem(Player player) {
            player.AddBuff(ModContent.BuffType<ChengYingBuff>(), 2); //2 tick, Mount 会自动刷新

            // 上剑仪式 (owner 客户端生成一次性纯视觉弹幕并同步)
            if (player.whoAmI == Main.myPlayer
                && player.ownedProjectileCounts[ModContent.ProjectileType<ChengYingSummonGlint>()] == 0) {
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<ChengYingSummonGlint>(), 0, 0f, player.whoAmI);
            }
            return true;
        }
    }

    /// <summary>
    /// 承影上剑仪式 (一次性纯视觉): 剑形从虚空溶解显形 + 垂直流光 + 淡青环纹。
    /// </summary>
    public class ChengYingSummonGlint : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int LifeTime = 26;

        private static Texture2D texSword;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            Projectile.Center = owner.Center + new Vector2(0f, 10f);

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40f, 12f),
                            DustID.IceTorch, new Vector2(0f, -Main.rand.NextFloat(1f, 2.6f)), 130, default, 1.2f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            texSword ??= ModContent.Request<Texture2D>(
                "AncientChineseMythology/Textures/Mounts/ChengYing/ChengYing", AssetRequestMode.ImmediateLoad).Value;
            if (texSword == null)
                return false;

            Player owner = Main.player[Projectile.owner];
            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            float pulse = MathHelper.Clamp(life < 0.3f ? life / 0.3f : 1f - (life - 0.3f) / 0.7f, 0f, 1f);

            // 影剑显形: 溶解从虚空凝出剑形再散去 (半透明折影)
            SpriteEffects flip = owner.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            WeaponVFX.ApplyDissolveBurn(texSword, Projectile.Center, null, new Color(170, 200, 230) * 0.8f,
                0f, texSword.Size() * 0.5f, 1.1f, threshold: MathF.Abs(0.65f - life) + 0.15f,
                intensity: pulse, edgeColor: new Color(210, 240, 255), edgeWidth: 0.12f, effects: flip);

            // 垂直流光 (拔地而起的一线白)
            if (ACMAsset.LightShot != null) {
                Color beam = new Color(220, 240, 255) * (pulse * 0.7f);
                beam.A = 0;
                Main.EntitySpriteDraw(ACMAsset.LightShot, Projectile.Center - Main.screenPosition, null, beam,
                    MathHelper.PiOver2, ACMAsset.LightShot.Size() * 0.5f,
                    new Vector2(1.6f * (0.5f + life), 0.5f), SpriteEffects.None, 0);
            }

            // 淡青环纹外扩
            WeaponVFX.DrawShockwaveRing(Projectile.Center, 12f + life * 58f, 8f, pulse * 0.8f,
                new Color(200, 235, 255), new Color(80, 120, 160));

            return false;
        }
    }
}

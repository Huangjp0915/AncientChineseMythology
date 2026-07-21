using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 熊掌震击 (黑熊幼灵落掌 AoE)。原地短寿命范围判定:
    /// 熊首虚影盖印 + 金辉冲击环 + 落掌闷响 + 震屏。ai[0]=1 为「金冠怒击」(范围/演出放大, 蜜琥珀飞溅)。
    /// 伤害仅前 10 帧生效 (判定窗口与视觉扩张严格对齐)。
    /// </summary>
    public class BlackBearStaffProj2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        private const int LifeTime = 26;
        private const int DamageWindow = 10; // 生效帧数 (从生成起)

        private bool Fury => Projectile.ai[0] == 1f;
        private float Life01 => 1f - Projectile.timeLeft / (float)LifeTime; // 0→1

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 76;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.light = 0.5f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40; // 寿命内单次判定
        }

        public override bool? CanDamage() => Projectile.timeLeft > LifeTime - DamageWindow ? null : false;

        public override void AI() {
            // 首帧: 判定放大(怒击) + 落掌音 + 震屏 + 尘土 (所有客户端同步演出)
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Fury)
                    Projectile.Resize(134, 106);

                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = -0.35f }, Projectile.Center);
                    if (Fury)
                        SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_2")
                            with { Volume = 0.35f, Pitch = -0.1f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, Fury ? 3f : 2f);

                    int dustCount = Fury ? 16 : 10;
                    for (int i = 0; i < dustCount; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(4.5f, 3f) - new Vector2(0f, 1.2f);
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 140, new Color(50, 48, 62), 1.4f);
                        d.noGravity = true;
                        if (i % 2 == 0) {
                            Dust g = Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin,
                                Main.rand.NextVector2Circular(3.5f, 3f), 100, default, 1.0f);
                            g.noGravity = true;
                        }
                        // 怒击: 蜜琥珀飞溅 (呼应黑熊精蜜雨)
                        if (Fury && i % 3 == 0) {
                            Dust h = Dust.NewDustPerfect(Projectile.Center, DustID.Honey,
                                Main.rand.NextVector2Circular(4f, 2.5f) - new Vector2(0f, 2f), 60, default, 1.2f);
                            h.noGravity = false;
                        }
                    }
                }
            }
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                Fury ? ACMWeaponBurst.Gold : ACMWeaponBurst.Bronze, scale: Fury ? 1.0f : 0.7f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = Life01;
            float sizeMul = Fury ? 1.4f : 1f;
            // 冲击环: poly ease-out 快扩 (一拍到位, 尾段驻留衰减)
            float expand = 1f - MathF.Pow(1f - Math.Min(life * 1.6f, 1f), 4f);
            float ringR = MathHelper.Lerp(12f, 74f, expand) * sizeMul;
            float alpha = MathHelper.Clamp(1.2f - life * 1.4f, 0f, 1f);

            WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 11f * sizeMul, alpha,
                new Color(255, 210, 110), new Color(95, 62, 22));
            if (Fury)
                WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR * 0.62f, 8f, alpha * 0.8f,
                    new Color(255, 185, 70), new Color(140, 90, 20));

            // 熊首虚影盖印: 加性金印, 微胀渐隐
            Texture2D head = TextureAssets.Projectile[Type].Value;
            float stampScale = (1.7f + life * 0.5f) * sizeMul;
            Color stamp = new Color(255, 205, 100) * (alpha * 0.85f);
            stamp.A = 0;
            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null, stamp,
                0f, head.Size() * 0.5f, stampScale, SpriteEffects.None, 0);

            // 心口柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, (0.9f + life * 0.5f) * sizeMul, new Color(255, 195, 90) * (alpha * 0.7f));

            return false;
        }
    }
}

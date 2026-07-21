using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 冥掌冲击波 —— 合掌夹击 / 旋冢收口 / 魂祭镇压的环形扩散波 (V3 重做)。
    /// 公平阀门: 出膛 14 帧速度 35%→100% 渐升 (防 telefrag), 无追踪;
    /// 从爆心向外扩散 → 爆心即安全芯。
    /// </summary>
    public class CorpsesClapWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;

        private ref float Timer => ref Projectile.ai[0];
        // localAI[0]: 出膛全速 (由初速反推, 各端自初速同步值计算, 一致)
        private ref float BaseSpeed => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI() {
            if (BaseSpeed == 0f)
                BaseSpeed = Projectile.velocity.Length();

            Timer++;

            // wind-up: 35% → 100% 渐升 (14f), 之后缓慢衰减拉开波环间距
            if (Timer <= 14f) {
                float ramp = MathHelper.Lerp(0.35f, 1f, Timer / 14f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * BaseSpeed * ramp;
            }
            else {
                Projectile.velocity *= 0.995f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.7f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 地府身份层: 冥掌冲击命中叠魂蚀
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.6f }, Projectile.position);
            if (!Main.dedServ) {
                for (int i = 0; i < 10; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(4f, 4f));
                    d.noGravity = true;
                    d.scale = 1.6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 双层拖尾: 外宽幽紫 + 内窄骨白
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color outer = (TelegraphColors.NetherViolet with { A = 0 }) * (progress * 0.5f);
                Main.EntitySpriteDraw(texture, drawPos, null, outer,
                    Projectile.oldRot[i], origin, Projectile.scale * (0.9f * progress + 0.2f), SpriteEffects.None);
            }

            // 波体: 鬼绿光晕 + 骨白核 (沿速度方向拉伸成掌波)
            Vector2 stretch = new(1.35f, 0.85f);
            Color glowC = TelegraphColors.GhostGreen with { A = 0 };
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                glowC * 0.55f, Projectile.rotation, origin, Projectile.scale * stretch * 1.25f, SpriteEffects.None);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                new Color(222, 240, 220, 160), Projectile.rotation, origin, Projectile.scale * stretch, SpriteEffects.None);

            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(200, 240, 210, 180);
        }
    }
}

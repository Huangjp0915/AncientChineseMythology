using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 城门闭合阶段的"预告落地骨雨"地标 (替换原暗影球乱射)。
    /// 先在地面亮起红色致命落点 (telegraph, §6.1 红=致命)，渐强后从上方降下骨雨。
    /// 纯地标本体不造成伤害；伤害由降下的 <see cref="CorpsesBoneShower"/> 与一次落地冲击承载。
    /// </summary>
    public class CorpsesBoneRainMarker : ModProjectile
    {
        // 复用既有占位纹理 (与同目录弹幕一致, 确保 autoload 安全)
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int TelegraphTime = 50; // 渐强预告 (~0.83s, 中等威胁)
        private const int ImpactWindow = 16;  // 落地命中窗口

        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphTime + ImpactWindow + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            // 预告阶段: 渐强红色落点尘 (可读)
            if (Timer < TelegraphTime) {
                if (!Main.dedServ) {
                    float t = Timer / TelegraphTime;
                    if (Main.rand.NextBool(2)) {
                        Vector2 off = new Vector2(Main.rand.NextFloat(-1f, 1f) * Projectile.width * 0.5f, 0f);
                        var d = Dust.NewDustPerfect(Projectile.Center + off, DustID.Torch);
                        d.noGravity = true;
                        d.scale = 0.8f + t * 1.2f;
                        d.velocity = new Vector2(0, -2f - t * 3f);
                    }
                }
            }
            // 落地: 降下骨雨 + 一次落地冲击
            else if (Timer == TelegraphTime) {
                SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.3f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -420f);
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), 9f);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, vel,
                            ModContent.ProjectileType<CorpsesBoneShower>(), Projectile.damage, 2f);
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 18; i++) {
                        int di = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                            DustID.Bone, 0, 0, 100, default, 1.6f);
                        Main.dust[di].velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f));
                    }
                }
                ACMUtils.AddScreenShake(4f);
            }
        }

        // 仅落地瞬间窗口造成伤害, 预告期完全无害 (telegraph 契约)
        public override bool CanHitPlayer(Player target) {
            return Timer >= TelegraphTime && Timer < TelegraphTime + ImpactWindow;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float t = MathHelper.Clamp(Timer / TelegraphTime, 0f, 1f);
            float flash = Timer < TelegraphTime ? t : MathHelper.Clamp(1f - (Timer - TelegraphTime) / (float)ImpactWindow, 0f, 1f);

            // 地面红色致命落点 (扁平条, 多层叠出柔边)
            Color warn = TelegraphColors.Lethal * (0.25f + 0.55f * flash);
            warn.A = 0;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float w = Projectile.width * (0.6f + 0.4f * t);
            for (int i = 0; i < 3; i++) {
                float ring = 1f - i * 0.28f;
                Main.EntitySpriteDraw(pixel, pos, new Rectangle(0, 0, 1, 1), warn * (0.35f + 0.2f * i),
                    0f, new Vector2(0.5f, 0.5f), new Vector2(w * ring, 7f * ring), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

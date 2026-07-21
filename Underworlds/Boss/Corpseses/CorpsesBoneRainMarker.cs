using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 骨雨审判地标 (V3 视觉升级)。
    /// 诚实落点预警: 渐强红色光柱 (CorpsesBoneRing uMode0) → 骨雨降下 + 一次落地冲击 (uMode1 冲击环)。
    /// 预告期完全无伤害; 仅落地窗口造成伤害 (telegraph 契约)。
    /// </summary>
    public class CorpsesBoneRainMarker : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int TelegraphTime = 50; // ≥ TelegraphColors.TelegraphTicks(Medium)
        private const int ImpactWindow = 16;

        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 80;
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

            // ai0 可负起步 (波次错拍): 负值期完全蛰伏
            if (Timer < 0f) {
                Projectile.timeLeft++;
                return;
            }

            if (Timer < TelegraphTime) {
                // 预告: 落点骨尘上扬渐强 (光柱由 PreDraw 绘制)
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    float t = Timer / TelegraphTime;
                    Vector2 off = new(Main.rand.NextFloat(-1f, 1f) * Projectile.width * 0.5f, 0f);
                    var d = Dust.NewDustPerfect(Projectile.Center + off, DustID.Torch);
                    d.noGravity = true;
                    d.scale = 0.8f + t * 1.2f;
                    d.velocity = new Vector2(0f, -2f - t * 3f);
                }
            }
            else if (Timer == TelegraphTime) {
                // 落地: 骨雨降下 + 一次冲击
                SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.3f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -430f);
                        Vector2 vel = new(Main.rand.NextFloat(-1.5f, 1.5f), 9f);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, vel,
                            ModContent.ProjectileType<CorpsesBoneShower>(), Projectile.damage, 2f, Main.myPlayer, 0f, 1f);
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 18; i++) {
                        var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f), DustID.Bone,
                            new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f)));
                        d.scale = 1.6f;
                    }
                }
                ACMUtils.AddScreenShake(4f);
            }
        }

        // 仅落地窗口造成伤害 (telegraph 契约)
        public override bool CanHitPlayer(Player target) {
            return Timer >= TelegraphTime && Timer < TelegraphTime + ImpactWindow;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || Timer < 0f)
                return false;

            if (Timer < TelegraphTime) {
                // 渐强红色落点光柱 (§6.1 红=致命)
                float prog = MathHelper.Clamp(Timer / TelegraphTime, 0f, 1f);
                Corpses.DrawBoneRingDecal(Main.spriteBatch, 0, Projectile.Center, 44f, 0.9f, prog,
                    Vector2.UnitX, 0f, TelegraphColors.Lethal, TelegraphColors.NetherViolet);
            }
            else {
                // 落地冲击环
                float p = MathHelper.Clamp((Timer - TelegraphTime) / (float)ImpactWindow, 0f, 1f);
                Corpses.DrawBoneRingDecal(Main.spriteBatch, 1, Projectile.Center, 210f, 1f, p,
                    Vector2.UnitX, 0f, new Color(225, 240, 220), TelegraphColors.GhostGreen);
            }
            return false;
        }
    }
}

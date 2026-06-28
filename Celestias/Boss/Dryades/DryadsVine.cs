using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精藤蔓/根须弹幕 (V2 多模态) — 复用原版荨麻纹理。
    ///  ai[0] 模式:
    ///   0 = 普通直线根须 (冒出放射 / 陷阱碎片)。
    ///   1 = 地面根须喷涌: 先 ~30 tick 地表光束预告 (DrawBeam, 翠绿→喷涌瞬间赤红), 后向上喷涌伤人。
    /// </summary>
    public class DryadsVine : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.NettleBurstRight}";

        private const int RootTelegraph = 30; // 根须喷涌预告时长
        private const int RootEruptLife = 42;  // 喷涌持续时长

        private float Mode => Projectile.ai[0];
        private bool erupted;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; // 根须需穿过地面
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.alpha = 30;
        }

        public override void AI() {
            if ((int)Mode == 1) {
                RootEruptAI();
                return;
            }

            // —— 模式 0: 普通直线根须 ——
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.velocity.Length() > 2f)
                Projectile.velocity *= 0.985f;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                    -Projectile.velocity.X * 0.05f, -Projectile.velocity.Y * 0.05f,
                    100, default, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.08f, 0.18f, 0.04f);
        }

        // —— 模式 1: 地面根须喷涌 (telegraph → erupt) ——
        private void RootEruptAI() {
            Projectile.rotation = -MathHelper.PiOver2; // 竖直向上
            Projectile.ai[1]++;
            int t = (int)Projectile.ai[1];

            if (t < RootTelegraph) {
                // 预告期: 不伤人 (光束预告由 PreDraw 绘制)
                Projectile.hostile = false;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = RootTelegraph + RootEruptLife + 4;

                if (Main.netMode != NetmodeID.Server && t % 4 == 0) {
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GreenTorch,
                        Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.5f, -0.3f), 80, default, 1.1f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.06f, 0.16f, 0.03f);
                return;
            }

            if (!erupted) {
                erupted = true;
                Projectile.hostile = true;
                Projectile.velocity = new Vector2(0f, -13f);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.6f, Volume = 0.7f }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-6f, -2f), 60, default, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            // 喷涌减速 (向上冲后回落感)
            Projectile.velocity.Y *= 0.96f;
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f), 90, default, 1.2f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.22f, 0.05f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 根须喷涌预告: 沿喷涌路径画光束 (翠绿 telegraph → 喷涌瞬间赤红致命)
            if ((int)Mode == 1 && !Main.dedServ) {
                int t = (int)Projectile.ai[1];
                if (t < RootTelegraph) {
                    float grow = t / (float)RootTelegraph;
                    Vector2 baseGround = Projectile.Center;
                    Vector2 tip = baseGround + new Vector2(0f, -MathHelper.Lerp(40f, 260f, grow));
                    // 临喷涌前 (grow>0.75) 转赤红
                    Color core = Color.Lerp(new Color(160, 255, 120), TelegraphColors.Lethal, MathHelper.Clamp((grow - 0.75f) / 0.25f, 0f, 1f));
                    Color edge = Color.Lerp(new Color(40, 120, 30), new Color(150, 20, 20), MathHelper.Clamp((grow - 0.75f) / 0.25f, 0f, 1f));
                    ACMShaders.DrawBeam(baseGround, tip, MathHelper.Lerp(8f, 22f, grow), core, edge, 0.4f + grow * 0.6f,
                        flowSpeed: 1.6f, flowScale: 2.2f);
                    return false; // 预告期不画藤蔓本体
                }
            }

            // 本体绘制
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color c = GetAlpha(lightColor) ?? lightColor;
            Main.spriteBatch.Draw(texture, drawPos, null, c, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(60, 140, 45, 180);
    }
}

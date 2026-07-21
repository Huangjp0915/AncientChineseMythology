using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·蜜雨 (V3) — 蜜雨咆哮召落的坠落蜜团: 落点地影红警渐强 45f (预警期无伤害) →
    /// 高速坠落致命 → 着地溅开并生成蜜潭 (场地机制)。教学"看地影 → 离开落点"。
    /// ai[0] = 预警计时 (自增); ai[1] = 基础伤害 (首帧捕获, 各端确定)。
    /// </summary>
    public class BlackBearHoneyDrip : ModProjectile
    {
        private const int WarnTicks = 45;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        private ref float WarnTimer => ref Projectile.ai[0];
        private ref float BaseDamage => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.tileCollide = false; // 自管着地
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
            Projectile.light = 0.3f;
        }

        // 计算正下方地面 Y (最近可阻挡固体块顶)
        private float GroundY() {
            int tileX = (int)MathHelper.Clamp(Projectile.Center.X / 16f, 1, Main.maxTilesX - 2);
            int tileY = (int)MathHelper.Clamp(Projectile.Center.Y / 16f, 1, Main.maxTilesY - 2);
            for (int y = tileY; y < Main.maxTilesY - 1; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return Projectile.Center.Y + 1200f;
        }

        public override void AI() {
            // 首帧捕获基础伤害 (ai 同步, 各端确定), 预警期置 0, 坠落期恢复
            if (WarnTimer == 0f)
                BaseDamage = Projectile.damage;
            WarnTimer++;

            if (WarnTimer < WarnTicks) {
                // 预警期: 高空悬停微浮, 无伤害
                Projectile.damage = 0;
                Projectile.velocity.Y = (float)Math.Sin(WarnTimer * 0.2f) * 0.6f;
                Projectile.velocity.X *= 0.9f;
                if (!Main.dedServ && WarnTimer % 6 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Honey, new Vector2(0, 1f), 80, default, 1.1f);
                    d.noGravity = true;
                }
            }
            else {
                // 坠落期: 加速 + 致命
                Projectile.damage = (int)BaseDamage;
                Projectile.velocity.Y += 0.55f;
                if (Projectile.velocity.Y > 18f)
                    Projectile.velocity.Y = 18f;
                Projectile.rotation = Projectile.velocity.ToRotation();

                // 高速坠落拖丝
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                        DustID.Honey, -Projectile.velocity * 0.06f, 90, default, 1.0f);
                    d.noGravity = true;
                }

                // 着地: 溅开 + 蜜潭
                if (Projectile.Bottom.Y >= GroundY()) {
                    Splatter();
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                            ModContent.ProjectileType<BlackBearHoneyPool>(), 0, 0f, Main.myPlayer, 100f);
                    }
                    Projectile.Kill();
                }
            }
        }

        private void Splatter() {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-5.5f, -1f));
                Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.Honey, vel, 80, default, 1.25f);
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (soft == null)
                return false;
            Vector2 origin = soft.Size() / 2f;

            // 落点地影红警 (渐强扁椭圆)
            if (WarnTimer < WarnTicks) {
                float prog = MathHelper.Clamp(WarnTimer / (float)WarnTicks, 0f, 1f);
                float gy = GroundY();
                Vector2 shadowPos = new Vector2(Projectile.Center.X, gy) - Main.screenPosition;
                Color warn = TelegraphColors.Lethal * (0.22f + 0.58f * prog);
                warn.A = 0;
                Main.spriteBatch.Draw(soft, shadowPos, null, warn, 0f, origin, new Vector2(0.45f + 0.5f * prog, 0.15f), SpriteEffects.None, 0f);
                // 中心亮芯 (落点精确位置)
                Color core = TelegraphColors.Flame * (0.4f * prog);
                core.A = 0;
                Main.spriteBatch.Draw(soft, shadowPos, null, core, 0f, origin, new Vector2(0.18f, 0.09f), SpriteEffects.None, 0f);
            }

            // 蜜团主体 (程序化: 柔光双层 + 高光, 坠落期沿速度拉伸)
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float stretch = 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.025f, 0f, 0.7f);
            float rot = WarnTimer >= WarnTicks ? Projectile.rotation : 0f;
            Vector2 scale = new(0.62f * stretch, 0.62f / stretch);

            Color outerC = new Color(150, 90, 15) * 0.85f; outerC.A = 0;
            Color bodyC = new Color(235, 160, 40) * 0.95f; bodyC.A = 0;
            Color hiC = new Color(255, 226, 140); hiC.A = 0;
            Main.spriteBatch.Draw(soft, drawPos, null, outerC, rot, origin, scale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(soft, drawPos, null, bodyC, rot, origin, scale * 0.8f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(soft, drawPos + new Vector2(-3f, -4f), null, hiC, 0f, origin, scale * 0.26f, SpriteEffects.None, 0f);
            return false;
        }
    }
}

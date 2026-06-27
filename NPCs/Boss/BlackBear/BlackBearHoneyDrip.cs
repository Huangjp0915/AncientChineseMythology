using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精 P2 场地灾害: 蜂蜜滴落 (V2 新增)。<b>落点地影预警的下落弹</b> —— 先在地面投影一圈渐强红影 (可读),
    /// 预警结束后高速坠落并致命, 着地溅开。教学"看地影 → 离开落点"的躲避语言。ai[0]=预警计时。
    /// </summary>
    public class BlackBearHoneyDrip : ModProjectile
    {
        private const int WarnTicks = 45; // 预警窗口 (地影渐强)

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.tileCollide = false; // 自管着地
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
            Projectile.light = 0.3f;
        }

        private ref float WarnTimer => ref Projectile.ai[0];
        private ref float BaseDamage => ref Projectile.ai[1];

        // 计算正下方地面 Y (找最近的可阻挡固体块顶)
        private float GroundY() {
            int tileX = (int)(Projectile.Center.X / 16f);
            int tileY = (int)(Projectile.Center.Y / 16f);
            tileX = (int)MathHelper.Clamp(tileX, 0, Main.maxTilesX - 1);
            for (int y = Math.Max(tileY, 0); y < Main.maxTilesY; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return Projectile.Center.Y + 1200f;
        }

        public override void AI() {
            // 首帧捕获基础伤害 (随生成同步), 预警期置 0, 坠落期恢复致命
            if (WarnTimer == 0f)
                BaseDamage = Projectile.damage;
            WarnTimer++;

            if (WarnTimer < WarnTicks) {
                // 预警期: 悬停轻微浮动, 无伤害
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
                Projectile.velocity.Y += 0.5f;
                if (Projectile.velocity.Y > 17f)
                    Projectile.velocity.Y = 17f;
                Projectile.rotation += 0.2f;

                // 着地检测
                if (Projectile.Bottom.Y >= GroundY()) {
                    Splatter();
                    Projectile.Kill();
                }
            }
        }

        private void Splatter() {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f));
                Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.Honey, vel, 80, default, 1.2f);
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 地面投影红影预警 (渐强)
            Texture2D soft = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (WarnTimer < WarnTicks && soft != null) {
                float prog = MathHelper.Clamp(WarnTimer / (float)WarnTicks, 0f, 1f);
                float gy = GroundY();
                Vector2 shadowPos = new Vector2(Projectile.Center.X, gy) - Main.screenPosition;
                Color warn = TelegraphColors.Lethal * (0.25f + 0.55f * prog);
                warn.A = 0;
                Vector2 origin = soft.Size() / 2f;
                Vector2 scale = new Vector2(0.5f + 0.5f * prog, 0.16f); // 扁椭圆地影
                Main.spriteBatch.Draw(soft, shadowPos, null, warn, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle src = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 originTex = src.Size() / 2f;
            Color tint = Color.Lerp(Color.White, TelegraphColors.Gold, 0.5f) * Projectile.Opacity;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, src, tint, Projectile.rotation, originTex, 0.45f, SpriteEffects.None, 0);
            return false;
        }
    }
}

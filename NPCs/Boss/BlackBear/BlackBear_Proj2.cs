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
    /// 黑熊精·蜜蜡弹 (V3) — 扑投甩出的抛物线粘稠蜜团。弧线全程可见可预判 (预警即弹道本身);
    /// 与地形碰撞, 着地溅开并留下一小片蜜潭 (场地机制入口)。
    /// 速度由 Boss 端弹道解算传入; 不再 OnSpawn 追踪玩家 (V2 曾凭 owner 瞄准 + 随机乱洒)。
    /// ai[0] = 蜜潭半径缩放 (0 视为 1)。
    /// </summary>
    public class BlackBear_Proj2 : ModProjectile
    {
        private const float Gravity = 0.30f;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.light = 0.35f;
        }

        public override void AI() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 粘稠拖丝 (节流)
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.Honey, -Projectile.velocity * 0.08f, 80, default, 1.0f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            // 溅开 + 留蜜潭 (服务器生成; 半径小于蜜雨潭)
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center,
                        DustID.Honey, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1f)), 60, default, 1.15f);
                    d.noGravity = false;
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                float poolScale = Projectile.ai[0] > 0.01f ? Projectile.ai[0] : 1f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<BlackBearHoneyPool>(), 0, 0f, Main.myPlayer, 78f * poolScale);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 程序化蜜团: 双层柔光 + 高光点, 沿速度方向轻微拉伸 (粘液感)
            Texture2D soft = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (soft == null)
                return false;
            Vector2 origin = soft.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speedStretch = 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.5f);
            Vector2 scale = new Vector2(0.62f * speedStretch, 0.62f / speedStretch) * Projectile.scale;

            // 拖尾残影
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = new Color(196, 122, 24) * (0.24f * fade);
                trail.A = 0;
                Main.spriteBatch.Draw(soft, old, null, trail, Projectile.rotation, origin, scale * (0.7f * fade), SpriteEffects.None, 0f);
            }

            Color outer = new Color(150, 90, 15) * 0.85f; outer.A = 0;
            Color body = new Color(235, 160, 40) * 0.95f; body.A = 0;
            Color highlight = new Color(255, 226, 140); highlight.A = 0;
            Main.spriteBatch.Draw(soft, drawPos, null, outer, Projectile.rotation, origin, scale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(soft, drawPos, null, body, Projectile.rotation, origin, scale * 0.8f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(soft, drawPos + new Vector2(-3f, -4f), null, highlight, 0f, origin, scale * 0.28f, SpriteEffects.None, 0f);
            return false;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
        }
    }
}

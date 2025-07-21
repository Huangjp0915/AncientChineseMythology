using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class ShenxianGuanglunPet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/Pets/ShenxianGuanglunPet";

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;               // 单帧
            ProjectileID.Sets.LightPet[Type] = true; // 光宠识别 :contentReference[oaicite:4]{index=4}
            Main.projPet[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.Wisp); // 仅复制基础属性，AI 将被覆盖
            Projectile.width = 32;   // 根据贴图实际像素调整
            Projectile.height = 32;
            Projectile.aiStyle = -1;  // 禁用 Wisp 原生 AI
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            int buffType = ModContent.BuffType<Buffs.ShenxianGuanglunBuff>();

            if (!player.HasBuff(buffType)) {
                if (player.dead) {
                    player.AddBuff(buffType, 18000); // 死亡保留
                }
                else {
                    Projectile.Kill();               // 主动取消 → 消失
                    return;
                }
            }

            // 没有主人 → 消失
            if (!player.active) {
                Projectile.Kill();
                return;
            }

            // 保证 Buff 标记（供 Player.ResetEffects 使用）
            player.GetModPlayer<ACMPlayer>().shenxianLightPet = true;

            /* 1️⃣ —— 定位：玩家头“后方” */
            Vector2 offset = new Vector2(-player.direction * 18f, -25f); // 水平 20px、垂直 -6px
            Projectile.Center = player.Center + offset;
            Projectile.velocity = Vector2.Zero;  // 不要惯性
            Projectile.rotation = 0f;            // 始终不旋转
            Projectile.spriteDirection = player.direction; // 跟随朝向翻转

            /* 2️⃣ —— 橘黄色光照 */
            Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.1f); // RGB 0-1 区间，类似暖火把 :contentReference[oaicite:5]{index=5}

            /* 3️⃣ —— 橘黄色粒子尾迹 */
            if (Main.rand.NextBool(6)) { // 约每 6 tick 一粒
                Dust d = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, 0f, 0f, 150, new Color(255, 120, 0), 0.9f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }

            /* 4️⃣ —— 生命周期防 despawn */
            Projectile.timeLeft = 2;  // 让它永远存在（由于每 tick 都设 2）
        }
    }
}
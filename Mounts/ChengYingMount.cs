using System.Linq;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Mounts
{
    public class ChengYingMount : ModMount
    {
        //public override string Texture =>  "AncientChineseMythology/Textures/Mounts/ChengYing/ChengYing";
        public override void SetStaticDefaults()
        {
            // ─────基础能力─────
            MountData.buff          = ModContent.BuffType<Buffs.ChengYingBuff>();
            MountData.spawnDust     = DustID.GemDiamond;
            MountData.heightBoost   = 34;   // 坐骑高度
            MountData.runSpeed      = 11f;
            MountData.dashSpeed     = 12f;
            MountData.acceleration  = 0.45f;
            MountData.jumpHeight    = 18;
            MountData.jumpSpeed     = 8f;
            MountData.flightTimeMax = int.MaxValue;
            MountData.fallDamage    = 0f;
            MountData.constantJump  = true;
            MountData.usesHover     = true; 
            MountData.fatigueMax    = 0;
            MountData.acceleration  = 0.4f;
            MountData.runSpeed      = 11f;

            // ─────帧参数─────
            MountData.totalFrames         = 1;
            MountData.standingFrameStart  = 0;
            MountData.standingFrameCount = 1; 
            MountData.runningFrameStart   = 0;
            MountData.inAirFrameStart     = 0;
            MountData.idleFrameStart      = 0;
            MountData.playerYOffsets      = new int[1] { 18 }; // 抬高玩家腰部

            // ─────贴图─────
            if (Main.netMode != NetmodeID.Server) {
                // 主纹理（必须有）
                MountData.backTexture = ModContent.Request<Texture2D>(
                    "AncientChineseMythology/Textures/Mounts/ChengYing/ChengYing");

                // 可选：前景层、Glow 层
                // AddTexture(TextureType.Front, "…_Front");
                // AddTexture(TextureType.GlowMask, "…_Glow");

                MountData.textureWidth  = 96;
                MountData.textureHeight = 34;
            }

            // ─────相对偏移─────
            MountData.xOffset          = 0;   // 水平居中
            MountData.yOffset          = 26;   // 整体略下沉
            MountData.playerHeadOffset = 0;
            MountData.bodyFrame        = 0;   // 站姿
        }

        public override void UpdateEffects(Player player) {
            // 2-1  添加幽兰色光源
            Lighting.AddLight(player.Center, 0.4f, 0.3f, 0.8f);   // RGB≈#664CCD:contentReference[oaicite:5]{index=5}

            // 2-2  每 1/3 tick 生成一颗拖尾 Dust
            if (Main.rand.NextBool(3)) {
                var dust = Dust.NewDustPerfect(
                    player.Center + new Vector2(-40 * player.direction, 0f),
                    DustID.ShimmerSpark,                       // 也可换自定义 Dust
                    new Vector2(0, Main.rand.NextFloat(-.5f, .5f)),
                    150, new Color(102, 76, 204), 1.2f);      // 幽兰
                dust.noGravity = true;
            }                                                // :contentReference[oaicite:6]{index=6}

            // 2-3  W / S 精确控制垂直速度
            const float climb = 0.35f, dive = 0.35f;
            if (player.controlUp)        player.velocity.Y -= climb;   // W 上升
            else if (player.controlDown) player.velocity.Y += dive;    // S 下降
            else if (!player.controlJump) player.velocity.Y *= 0.9f;   // 无输入时渐停

            // 可选：同时禁用翅膀条，避免逻辑冲突
            player.wingTime = player.wingTimeMax = 0;

            if (player.whoAmI == Main.myPlayer) {   // 只在本机生成
                if (!Main.projectile.Any(p => p.active &&
                    p.type == ModContent.ProjectileType<ChengYingHitbox>() &&
                    p.owner == player.whoAmI))
                {
                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        player.Center, Vector2.Zero,
                        ModContent.ProjectileType<ChengYingHitbox>(),
                        60, 6f, player.whoAmI);
                }
            }
        }
    }
}

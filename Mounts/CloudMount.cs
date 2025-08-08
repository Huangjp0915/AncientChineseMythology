using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Mounts
{
    public class CloudMount : ModMount
    {
        /*──────── 常量区 ────────*/
        private const float VertSpeed = 0.35f;   // 垂直推力

        public override void SetStaticDefaults() {
            /*──基础属性──*/
            MountData.buff = ModContent.BuffType<Buffs.CloudMountBuff>();
            MountData.spawnDust = DustID.Cloud;
            MountData.heightBoost = 60;
            MountData.runSpeed = 8f;
            MountData.dashSpeed = 8f;
            MountData.acceleration = 0.25f;
            MountData.jumpHeight = 10;
            MountData.jumpSpeed = 6f;
            MountData.flightTimeMax = int.MaxValue;  // 无限飞
            MountData.constantJump = true;          // 按住空格可持续上升
            MountData.usesHover = true;
            MountData.fallDamage = 0f;

            /*──帧设置──*/
            MountData.totalFrames = 1;
            MountData.standingFrameStart = 0;
            MountData.standingFrameCount = 1;
            MountData.runningFrameStart = 0;
            MountData.inAirFrameStart = 0;
            MountData.idleFrameStart = 0;
            MountData.playerYOffsets = new int[1] { 40 }; // 抬高玩家腰部

            /*──贴图──*/
            if (!Main.dedServ) {
                MountData.frontTexture = ModContent.Request<Texture2D>(
                    "AncientChineseMythology/Textures/Mounts/Cloud/CloudMount");
                MountData.textureWidth = 150;
                MountData.textureHeight = 64;
            }

            /*──偏移──*/
            MountData.xOffset = 0;
            MountData.yOffset = 32;
            MountData.bodyFrame = 0;
        }

        /*──────── 粒子 + 垂直控制 ────────*/
        public override void UpdateEffects(Player player) {
            /*云雾粒子*/
            /*if (Main.rand.NextBool(4)) {
                Dust a = Dust.NewDustPerfect(
                    player.Center + new Vector2(-40f * player.direction, 40f),
                    DustID.Cloud,
                    new Vector2(Main.rand.NextFloat(-.2f, .2f),
                                Main.rand.NextFloat(-1f, -.3f)),
                    100, Color.White, 1.2f);
                Dust b = Dust.NewDustPerfect(
                    player.Center + new Vector2(-35f * player.direction, 40f),
                    DustID.Cloud,
                    new Vector2(Main.rand.NextFloat(-.2f, .2f),
                                Main.rand.NextFloat(-1f, -.3f)),
                    100, Color.White, 1.2f);
                Dust c = Dust.NewDustPerfect(
                    player.Center + new Vector2(-40f * player.direction, 35f),
                    DustID.Cloud,
                    new Vector2(Main.rand.NextFloat(-.2f, .2f),
                                Main.rand.NextFloat(-1f, -.3f)),
                    100, Color.White, 1.2f);
                Dust d = Dust.NewDustPerfect(
                    player.Center + new Vector2(-35f * player.direction, 35f),
                    DustID.Cloud,
                    new Vector2(Main.rand.NextFloat(-.2f, .2f),
                                Main.rand.NextFloat(-1f, -.3f)),
                    100, Color.White, 1.2f);
                a.noGravity = true;
                b.noGravity = true;
                c.noGravity = true;
                d.noGravity = true;
            }*/

            /*W / S / Space 垂直操控*/
            bool wantAscend = player.controlUp || player.controlJump; // W 或 空格
            bool wantDescend = player.controlDown;
            bool onGround = player.velocity.Y == 0f &&
                            Collision.SolidCollision(
                                player.position + new Vector2(0, player.height),
                                player.width, 2);

            if (onGround && wantAscend) {        // 贴地起跳
                player.velocity.Y = -6f;
                player.fallStart = (int)(player.position.Y / 16f);
            }

            if (wantAscend) player.velocity.Y -= VertSpeed;
            else if (wantDescend) player.velocity.Y += VertSpeed;
            else player.velocity.Y *= .95f;

            /*防止翅膀条与坐骑冲突*/
            player.wingTime = player.wingTimeMax = 0;
        }
    }
}

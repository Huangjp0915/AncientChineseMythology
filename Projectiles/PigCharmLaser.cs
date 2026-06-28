using AncientChineseMythology.Helpers;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class PigCharmLaser : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/PigCharmLaser";

        //定义状态枚举：0=发射，1=持续，2=结束
        private enum LaserState
        {
            Firing = 0,
            Continuous = 1,
            Ending = 2
        }

        //三个贴图路径
        private const string FiringTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Firing";
        private const string ContinuousTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Continuous";
        private const string EndingTexturePath = "AncientChineseMythology/Textures/Projectiles/PigCharmLaser_Ending";

        //每个动画状态的帧数（均为8帧）
        private const int FiringFrameCount = 8;
        private const int ContinuousFrameCount = 8;
        private const int EndingFrameCount = 8;
        //每帧持续时间（每隔 FrameDuration 帧切换动画帧）
        private const int FrameDuration = 5;

        public override void SetStaticDefaults() {
            //初始状态使用发射动画的帧数
            Main.projFrames[Projectile.type] = FiringFrameCount;
        }

        public override void SetDefaults() {
            Projectile.width = 10;  //初始尺寸，实际碰撞框在 AI 中更新
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1; //命中一个敌人后结束
            //设为 false，因为手动检测地形阻挡
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3600;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
            //初始状态为 Firing
            Projectile.localAI[0] = (int)LaserState.Firing;
            //用于记录扣魔力计时（单位帧），初始为0
            Projectile.ai[0] = 0f;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (player.dead) {
                Projectile.Kill();
                return;
            }

            //添加蓝色亮光
            Lighting.AddLight(Projectile.Center, new Vector3(0f, 0f, 1f));

            //计算玩家中心到鼠标的理想距离与方向
            Vector2 origin = player.Center;
            Vector2 diff = Main.MouseWorld - origin;
            float idealDistance = diff.Length();
            if (idealDistance < 0.0001f) {
                idealDistance = 0.0001f;
                diff = Vector2.UnitY;
            }
            else {
                diff.Normalize();
            }

            //地形阻挡检测：逐步检测，若遇到实心砖块，则截断激光
            float step = 8f; //检测步长
            float effectiveDistance = idealDistance;
            for (float d = 0; d < idealDistance; d += step) {
                Vector2 checkPos = origin + diff * d;
                if (Collision.SolidCollision(checkPos, 1, 1)) {
                    effectiveDistance = d - 1f;
                    break;
                }
            }
            if (effectiveDistance < 8f)
                effectiveDistance = 8f;
            Projectile.ai[1] = effectiveDistance;

            //在激光沿途添加蓝色光照
            float lightStep = 16f;
            for (float d = 0; d < effectiveDistance; d += lightStep) {
                Vector2 lightPos = origin + diff * d;
                Lighting.AddLight(lightPos, new Vector3(0.2f, 0.4f, 1f));
            }
            Vector2 endPos = origin + diff * effectiveDistance;
            Lighting.AddLight(endPos, new Vector3(0.2f, 0.4f, 1f));

            //设置激光位置、旋转
            Projectile.rotation = diff.ToRotation() - MathHelper.PiOver2;
            Projectile.position = origin;
            Projectile.velocity = Vector2.Zero;

            //状态切换处理
            LaserState state = (LaserState)(int)Projectile.localAI[0];
            if (state == LaserState.Firing) {
                if (!player.channel) {
                    EnterEndingState();
                }
                else {
                    //每次AI调用增加0.5f
                    Projectile.ai[0] += 0.5f;
                    if (Projectile.ai[0] >= 60f) {
                        Projectile.ai[0] = 0f;
                        if (player.statMana >= 10) {
                            player.statMana -= 10;
                        }
                        else {
                            EnterEndingState();
                        }
                    }

                    if (Projectile.frame >= FiringFrameCount - 1 && Projectile.frameCounter >= FrameDuration - 1) {
                        EnterContinuousState();
                    }
                }
            }
            else if (state == LaserState.Continuous) {
                if (!player.channel) {
                    EnterEndingState();
                }
                else {
                    Projectile.ai[0] += 0.5f;
                    if (Projectile.ai[0] >= 60f) {
                        Projectile.ai[0] = 0f;
                        if (player.statMana >= 10) {
                            player.statMana -= 10;
                        }
                        else {
                            EnterEndingState();
                        }
                    }
                }
            }
            else if (state == LaserState.Ending) {
                if (Projectile.frame >= EndingFrameCount - 1 && Projectile.frameCounter >= FrameDuration - 1) {
                    Projectile.Kill();
                    return;
                }
            }

            //高度 = effectiveDistance, 宽度固定（10像素）
            Projectile.width = 10;
            Projectile.height = (int)effectiveDistance;

            UpdateAnimationFrames();
        }

        private void UpdateAnimationFrames() {
            LaserState state = (LaserState)(int)Projectile.localAI[0];
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= FrameDuration) {
                Projectile.frameCounter = 0;
                if (state == LaserState.Continuous) {
                    //持续状态循环播放
                    Projectile.frame = (Projectile.frame + 1) % ContinuousFrameCount;
                }
                else if (state == LaserState.Firing) {
                    Projectile.frame++;
                    if (Projectile.frame >= FiringFrameCount) {
                        EnterContinuousState();
                    }
                }
                else if (state == LaserState.Ending) {
                    Projectile.frame++;
                    if (Projectile.frame >= EndingFrameCount) {
                        Projectile.frame = EndingFrameCount - 1;
                    }
                }
            }
        }

        private void EnterContinuousState() {
            Projectile.localAI[0] = (int)LaserState.Continuous;
            Projectile.frame = 0;
            Projectile.frameCounter = 0;
            Main.projFrames[Projectile.type] = ContinuousFrameCount;
        }

        private void EnterEndingState() {
            if ((LaserState)(int)Projectile.localAI[0] != LaserState.Ending) {
                Projectile.localAI[0] = (int)LaserState.Ending;
                Projectile.frame = 0;
                Projectile.frameCounter = 0;
                Main.projFrames[Projectile.type] = EndingFrameCount;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Player player = Main.player[Projectile.owner];
            Vector2 start = player.Center;
            float effectiveDistance = Projectile.ai[1];
            Vector2 end = start + (Main.MouseWorld - start).SafeNormalize(Vector2.UnitY) * effectiveDistance;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 10f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中点金辉演出 (一次性, 不频繁)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 0.5f, owner: Projectile.owner);
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 表现层重做: 用共享 BeamGrad 流动渐变光束 + 端点柔光取代逐帧动画贴图。
            Player player = Main.player[Projectile.owner];
            float effectiveDistance = Projectile.ai[1];
            LaserState state = (LaserState)(int)Projectile.localAI[0];

            Vector2 start = player.Center;
            Vector2 diff = (Main.MouseWorld - start).SafeNormalize(Vector2.UnitY);
            Vector2 end = start + diff * effectiveDistance;

            // 结束态随帧淡出
            float fade = 1f;
            if (state == LaserState.Ending)
                fade = 1f - MathHelper.Clamp(Projectile.frame / (float)EndingFrameCount, 0f, 1f);
            float pulse = (0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f)) * fade;
            if (pulse <= 0.02f)
                return false;

            ACMShaders.DrawBeam(start, end, 12f * fade,
                new Color(255, 230, 150, 210), new Color(210, 130, 40, 130), pulse,
                flowSpeed: 2.2f, flowScale: 2.5f);
            WeaponVFX.DrawGlowBurst(end, 0.7f * fade, new Color(255, 215, 120) * fade);
            WeaponVFX.DrawGlowBurst(start, 0.5f * fade, new Color(255, 225, 150) * fade);
            return false;
        }
    }
}

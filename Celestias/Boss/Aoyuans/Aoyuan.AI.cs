using System;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    internal partial class Aoyuan
    {
        #region AI主循环

        public override bool PreAI() {
            globalTime += 1f / 60f;

            if (divebombCooldown > 0)
                divebombCooldown--;

            // 激活天空背景
            if (!VaultUtils.isServer && AoyuanSky.name != null) {
                if (!SkyManager.Instance[AoyuanSky.name].IsActive())
                    SkyManager.Instance.Activate(AoyuanSky.name, NPC.Center);
            }

            Player player = Main.player[NPC.target];

            // 攻击帧动画
            if (fireAttack || internalAI[0] >= 450) {
                attackCounter++;
                if (attackCounter > 10) {
                    attackFrame++;
                    attackCounter = 0;
                }
                if (attackFrame >= 3)
                    attackFrame = 2;
            }

            float dist = NPC.Distance(player.Center);

            // 攻击计时
            internalAI[0]++;
            if (internalAI[0] == 350) {
                internalAI[1] = Main.rand.Next(3);
            }
            if (internalAI[0] > 300) {
                Attack(NPC);
            }
            if (internalAI[0] >= 400) {
                internalAI[0] = 0;
            }

            // 触发龙息攻击
            if (dist > 300 && Main.rand.NextBool(20) && !fireAttack && internalAI[0] < 500) {
                fireAttack = true;
            }

            if (fireAttack) {
                attackTimer++;
                // 冰息粒子
                if (!VaultUtils.isServer && attackTimer % 5 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustVel = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 6);
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y, 180, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }
                if (attackTimer >= 80) {
                    fireAttack = false;
                    attackTimer = 0;
                    attackFrame = 0;
                    attackCounter = 0;
                }
            }

            // 出生粒子
            if (NPC.alpha > 0) {
                for (int spawnDust = 0; spawnDust < 2; spawnDust++) {
                    int d = Dust.NewDust(new Vector2(NPC.position.X, NPC.position.Y), NPC.width, NPC.height,
                        DustID.IceTorch, 0f, 0f, 100, default, 2f);
                    Main.dust[d].noGravity = true;
                }
                NPC.alpha -= 12;
                if (NPC.alpha < 0) NPC.alpha = 0;
            }

            // 朝向
            NPC.spriteDirection = NPC.velocity.X > 0 ? -1 : 1;

            NPC.ai[1]++;
            if (NPC.ai[1] >= 1200)
                NPC.ai[1] = 0;

            NPC.TargetClosest(true);
            if (!Main.player[NPC.target].active || Main.player[NPC.target].dead) {
                NPC.TargetClosest(true);
                if (!Main.player[NPC.target].active || Main.player[NPC.target].dead) {
                    // 关闭天空
                    if (!VaultUtils.isServer && AoyuanSky.name != null) {
                        SkyManager.Instance.Deactivate(AoyuanSky.name);
                    }
                    NPC.ai[3]++;
                    NPC.velocity.Y += 0.11f;
                    if (NPC.ai[3] >= 300)
                        NPC.active = false;
                }
                else {
                    NPC.ai[3] = 0;
                }
            }

            // 蠕虫身体链生成（只在ai[0]==0时初始化一次）
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (NPC.ai[0] == 0) {
                    NPC.realLife = NPC.whoAmI;
                    int latestNPC = NPC.whoAmI;
                    for (int i = 0; i < BodyFrameSequence.Length; ++i) {
                        latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                            ModContent.NPCType<AoyuanBody>(), NPC.whoAmI, 0, latestNPC);
                        Main.npc[latestNPC].realLife = NPC.whoAmI;
                        Main.npc[latestNPC].ai[3] = NPC.whoAmI;
                        Main.npc[latestNPC].netUpdate = true;
                        Main.npc[latestNPC].ai[2] = BodyFrameSequence[i];
                    }
                    NPC.ai[0] = 1;
                    NPC.netUpdate2 = true;
                }
            }

            // 蠕虫移动AI - 追踪玩家
            float speed = 12f;
            float acceleration = 0.13f;

            Vector2 npcCenter = new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height * 0.5f);
            float targetXPos = Main.player[NPC.target].position.X + Main.player[NPC.target].width / 2;
            float targetYPos = Main.player[NPC.target].position.Y + Main.player[NPC.target].height / 2;

            float targetRoundedPosX = (int)(targetXPos / 16.0) * 16;
            float targetRoundedPosY = (int)(targetYPos / 16.0) * 16;
            npcCenter.X = (int)(npcCenter.X / 16.0) * 16;
            npcCenter.Y = (int)(npcCenter.Y / 16.0) * 16;
            float dirX = targetRoundedPosX - npcCenter.X;
            float dirY = targetRoundedPosY - npcCenter.Y;

            float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);

            if (NPC.soundDelay == 0) {
                float num1 = length / 40f;
                if (num1 < 10.0) num1 = 10f;
                if (num1 > 20.0) num1 = 20f;
                NPC.soundDelay = (int)num1;
            }

            float absDirX = Math.Abs(dirX);
            float absDirY = Math.Abs(dirY);
            float newSpeed = speed / length;
            dirX *= newSpeed;
            dirY *= newSpeed;

            if ((NPC.velocity.X > 0.0 && dirX > 0.0) || (NPC.velocity.X < 0.0 && dirX < 0.0) ||
                (NPC.velocity.Y > 0.0 && dirY > 0.0) || (NPC.velocity.Y < 0.0 && dirY < 0.0)) {
                if (NPC.velocity.X < dirX) NPC.velocity.X += acceleration;
                else if (NPC.velocity.X > dirX) NPC.velocity.X -= acceleration;
                if (NPC.velocity.Y < dirY) NPC.velocity.Y += acceleration;
                else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= acceleration;

                if (Math.Abs(dirY) < speed * 0.2 && ((NPC.velocity.X > 0.0 && dirX < 0.0) || (NPC.velocity.X < 0.0 && dirX > 0.0))) {
                    if (NPC.velocity.Y > 0.0) NPC.velocity.Y += acceleration * 2f;
                    else NPC.velocity.Y -= acceleration * 2f;
                }
                if (Math.Abs(dirX) < speed * 0.2 && ((NPC.velocity.Y > 0.0 && dirY < 0.0) || (NPC.velocity.Y < 0.0 && dirY > 0.0))) {
                    if (NPC.velocity.X > 0.0) NPC.velocity.X += acceleration * 2f;
                    else NPC.velocity.X -= acceleration * 2f;
                }
            }
            else if (absDirX > absDirY) {
                if (NPC.velocity.X < dirX) NPC.velocity.X += acceleration * 1.1f;
                else if (NPC.velocity.X > dirX) NPC.velocity.X -= acceleration * 1.1f;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5) {
                    if (NPC.velocity.Y > 0.0) NPC.velocity.Y += acceleration;
                    else NPC.velocity.Y -= acceleration;
                }
            }
            else {
                if (NPC.velocity.Y < dirY) NPC.velocity.Y += acceleration * 1.1f;
                else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= acceleration * 1.1f;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5) {
                    if (NPC.velocity.X > 0.0) NPC.velocity.X += acceleration;
                    else NPC.velocity.X -= acceleration;
                }
            }

            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;

            // 玩家死亡/太远则脱战
            if (Main.player[NPC.target].dead ||
                Math.Abs(NPC.position.X - Main.player[NPC.target].position.X) > 6000f ||
                Math.Abs(NPC.position.Y - Main.player[NPC.target].position.Y) > 6000f) {
                NPC.velocity.Y -= 1f;
                if (NPC.position.Y < 0) {
                    NPC.velocity.Y -= 1f;
                }
                if (NPC.position.Y < 0) {
                    for (int i = 0; i < 200; i++) {
                        if (Main.npc[i].aiStyle == NPC.aiStyle)
                            Main.npc[i].active = false;
                    }
                }
            }

            if ((NPC.velocity.X > 0.0 && NPC.oldVelocity.X < 0.0 || NPC.velocity.X < 0.0 && NPC.oldVelocity.X > 0.0 ||
                 NPC.velocity.Y > 0.0 && NPC.oldVelocity.Y < 0.0 || NPC.velocity.Y < 0.0 && NPC.oldVelocity.Y > 0.0) && !NPC.justHit)
                NPC.netUpdate = true;

            // 冰霜光照
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.9f) * glowIntensity);

            return false;
        }

        #endregion

        #region 攻击逻辑

        private void Attack(NPC npc) {
            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 2;

            if (internalAI[1] == 0) {
                // 冰柱雨
                if (internalAI[0] == 320 || internalAI[0] == 340 || internalAI[0] == 360 || internalAI[0] == 380) {
                    int count = Main.expertMode ? 10 : 8;
                    for (int i = 0; i < count; i++) {
                        AoyuanAttacks.IcicleRain(npc);
                    }
                }
            }
            else if (internalAI[1] == 1) {
                // 前方冰弹
                if (internalAI[0] == 350 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center.X, npc.Center.Y, npc.velocity.X * 2, npc.velocity.Y,
                        ModContent.ProjectileType<AoyuanIceball>(), damage, 3f, Main.myPlayer);
                }
            }
            else {
                // 扇形冰弹
                if (internalAI[0] == 350) {
                    int count = Main.expertMode ? 6 : 10;
                    AoyuanAttacks.IceBurst(npc, count);
                }
            }
        }

        #endregion
    }
}

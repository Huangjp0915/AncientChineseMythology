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

        // 攻击类型常量
        // internalAI[1] 值对应:
        // 0 = 冰柱雨（多波次）
        // 1 = 前方冰弹
        // 2 = 扇形冰晶散射
        // 3 = 螺旋冰弹
        // 4 = 追踪冰弹连射
        // 5 = 冰霜环 + 冰柱雨组合
        // 6 = 龙息冰锥连射
        // 7 = 冰柱激光大招（二阶段专属）

        /// <summary>一阶段攻击类型数</summary>
        private const int Phase1AttackCount = 5;
        /// <summary>二阶段攻击类型数</summary>
        private const int Phase2AttackCount = 8;

        public override bool PreAI() {
            globalTime += 1f / 60f;

            if (divebombCooldown > 0)
                divebombCooldown--;
            if (beamCooldown > 0)
                beamCooldown--;

            // 激活天空背景
            if (!VaultUtils.isServer && AoyuanSky.name != null) {
                if (!SkyManager.Instance[AoyuanSky.name].IsActive())
                    SkyManager.Instance.Activate(AoyuanSky.name, NPC.Center);
            }

            Player player = Main.player[NPC.target];

            // 攻击帧动画（龙息/大招时张嘴）
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

            // 攻击计时器循环
            internalAI[0]++;

            // 选择下一次攻击类型
            if (internalAI[0] == 350) {
                if (IsPhase2) {
                    // 二阶段：激光大招有冷却
                    if (beamCooldown <= 0 && Main.rand.NextBool(4)) {
                        internalAI[1] = 7; // 冰柱激光
                    }
                    else {
                        internalAI[1] = Main.rand.Next(Phase2AttackCount - 1); // 0-6
                    }
                }
                else {
                    internalAI[1] = Main.rand.Next(Phase1AttackCount); // 0-4
                }
            }

            // 执行攻击
            if (internalAI[0] > 300) {
                Attack(NPC);
            }
            if (internalAI[0] >= 400) {
                internalAI[0] = 0;
                breathBurstCount = 0;
            }

            // 龙息攻击（距离远时触发张嘴喷冰）
            if (dist > 300 && Main.rand.NextBool(20) && !fireAttack && internalAI[0] < 300) {
                fireAttack = true;
            }

            if (fireAttack) {
                attackTimer++;
                // 冰息粒子
                if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                    Vector2 breathDir = NPC.velocity.SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 4; i++) {
                        Vector2 dustVel = breathDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(4, 8);
                        int d = Dust.NewDust(NPC.Center + breathDir * 40f, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y, 180, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }
                // 龙息期间发射冰锥
                if (attackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    AoyuanAttacks.BreathIcicles(NPC, Main.expertMode ? 4 : 2);
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

            // 蠕虫身体链生成
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

            // 蠕虫移动AI - 二阶段加速
            float speed = IsPhase2 ? 16f : 12f;
            float acceleration = IsPhase2 ? 0.18f : 0.13f;

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

            // 脱战
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

            // 冰霜光照（二阶段更亮）
            float lightMul = IsPhase2 ? 1.5f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.9f) * glowIntensity * lightMul);

            return false;
        }

        #endregion

        #region 攻击逻辑

        private void Attack(NPC npc) {
            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 2;

            switch ((int)internalAI[1]) {
                case 0:
                    // 冰柱雨（多波次下落）
                    if (internalAI[0] == 320 || internalAI[0] == 340 || internalAI[0] == 360 || internalAI[0] == 380) {
                        int count = Main.expertMode ? 10 : 8;
                        if (IsPhase2) count += 4;
                        for (int i = 0; i < count; i++) {
                            AoyuanAttacks.IcicleRain(npc);
                        }
                        SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 0.7f }, npc.Center);
                    }
                    break;

                case 1:
                    // 前方冰弹
                    if (internalAI[0] == 350 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            npc.Center.X, npc.Center.Y, npc.velocity.X * 2, npc.velocity.Y,
                            ModContent.ProjectileType<AoyuanIceball>(), damage, 3f, Main.myPlayer);
                    }
                    break;

                case 2:
                    // 扇形冰晶散射
                    if (internalAI[0] == 350) {
                        int count = Main.expertMode ? 8 : 6;
                        if (IsPhase2) count += 4;
                        AoyuanAttacks.IceBurst(npc, count);
                        SoundEngine.PlaySound(SoundID.Item67 with { Pitch = 0.2f }, npc.Center);
                    }
                    break;

                case 3:
                    // 螺旋冰弹（持续释放旋转弹幕）
                    if (internalAI[0] >= 310 && internalAI[0] <= 390 && (int)internalAI[0] % 8 == 0) {
                        float spinAngle = (internalAI[0] - 310) * 0.15f;
                        int arms = IsPhase2 ? 5 : 3;
                        float spd = Main.expertMode ? 10f : 8f;
                        AoyuanAttacks.SpiralIce(npc, spinAngle, arms, spd);
                    }
                    break;

                case 4:
                    // 追踪冰弹连射（3波）
                    if ((internalAI[0] == 320 || internalAI[0] == 350 || internalAI[0] == 380)) {
                        int count = Main.expertMode ? 6 : 4;
                        if (IsPhase2) count += 2;
                        AoyuanAttacks.HomingBurst(npc, count);
                        SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.2f }, npc.Center);
                    }
                    break;

                case 5:
                    // 冰霜环 + 冰柱雨组合技
                    if (internalAI[0] == 330) {
                        int ringCount = IsPhase2 ? 24 : 16;
                        AoyuanAttacks.FrostRing(npc, ringCount, 5f);
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f, Volume = 1.2f }, npc.Center);
                    }
                    if (internalAI[0] == 360) {
                        int ringCount = IsPhase2 ? 24 : 16;
                        AoyuanAttacks.FrostRing(npc, ringCount, 7f);
                    }
                    if (internalAI[0] >= 340 && internalAI[0] <= 380 && (int)internalAI[0] % 10 == 0) {
                        AoyuanAttacks.IcicleStorm(npc, IsPhase2 ? 4 : 2);
                    }
                    break;

                case 6:
                    // 龙息冰锥连射（张嘴持续喷射）
                    if (internalAI[0] == 310) {
                        fireAttack = true;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                        SoundEngine.PlaySound(SoundID.NPCDeath60 with { Pitch = 0.3f, Volume = 1.2f }, npc.Center);
                    }
                    if (internalAI[0] >= 310 && internalAI[0] <= 390 && (int)internalAI[0] % 6 == 0) {
                        int count = Main.expertMode ? 4 : 2;
                        AoyuanAttacks.BreathIcicles(npc, count);
                        breathBurstCount++;
                    }
                    if (internalAI[0] == 390) {
                        fireAttack = false;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                    }
                    break;

                case 7:
                    // 冰柱激光大招（二阶段专属）
                    if (internalAI[0] == 320) {
                        fireAttack = true;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                        AoyuanAttacks.FrostBeam(npc);
                        beamCooldown = 1200;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, npc.Center);
                    }
                    if (internalAI[0] == 395) {
                        fireAttack = false;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                    }
                    break;
            }
        }

        #endregion
    }
}

using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region AI主循环

        // 攻击类型常量
        // internalAI[1] 值对应:
        // 0 = 雷柱雨（多波次）
        // 1 = 前方雷球
        // 2 = 扇形雷球散射
        // 3 = 螺旋雷球
        // 4 = 追踪雷球连射
        // 5 = 雷电环 + 雷柱雨组合
        // 6 = 龙息雷锥连射
        // 7 = 雷柱激光大招（二阶段专属）

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
            if (!VaultUtils.isServer && AoshunSky.name != null) {
                if (!SkyManager.Instance[AoshunSky.name].IsActive())
                    SkyManager.Instance.Activate(AoshunSky.name, NPC.Center);
            }

            // 先选定目标再判定脱战
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            // 玩家死亡则脱战（不限制白天/黑夜）
            if (!player.active || player.dead) {
                despawn = true;
            }
            if (despawn) {
                NPC.velocity.Y += 0.11f;
                NPC.ai[3]++;
                if (NPC.ai[3] >= 300) {
                    NPC.active = false;
                    if (!VaultUtils.isServer && AoshunSky.name != null) {
                        SkyManager.Instance.Deactivate(AoshunSky.name);
                    }
                }
                return false;
            }

            // 参考原型: 近距离判定（close时张嘴+喷息）
            if (Vector2.Distance(NPC.Center, player.Center) <= 400) {
                close = true;
            }
            else {
                close = false;
            }

            // 近距离时喷射龙息弹幕（参考原型的breath逻辑）
            if (close) {
                Vector2 mouthPos = new Vector2(NPC.position.X + NPC.width / 2, NPC.position.Y + NPC.height / 2);
                if (Main.rand.NextBool(7)) {
                    int damage = Main.expertMode ? 35 : 50;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), mouthPos.X + NPC.velocity.X, mouthPos.Y + NPC.velocity.Y,
                        NPC.velocity.X * 0.8f + Main.rand.NextFloat(-0.7f, 0.7f) * 3,
                        NPC.velocity.Y * 0.8f + Main.rand.NextFloat(-0.7f, 0.7f) * 3,
                        ModContent.ProjectileType<AoshunThunderball>(), damage, 0f, Main.myPlayer);
                    if (Main.rand.NextBool(2)) {
                        SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath, NPC.position);
                    }
                }
            }

            // 攻击计时器循环
            internalAI[0]++;

            // 选择下一次攻击类型
            if (internalAI[0] == 350) {
                if (IsPhase2) {
                    if (beamCooldown <= 0 && Main.rand.NextBool(4)) {
                        internalAI[1] = 7;
                    }
                    else {
                        internalAI[1] = Main.rand.Next(Phase2AttackCount - 1);
                    }
                }
                else {
                    internalAI[1] = Main.rand.Next(Phase1AttackCount);
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

            // 参考原型: 蠕虫身体链生成（Body和Arms交替 + Tail结尾）
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (NPC.ai[0] == 0) {
                    NPC.realLife = NPC.whoAmI;
                    int latestNPC = NPC.whoAmI;
                    int randomWormLength = Main.rand.Next(25, 35);
                    for (int i = 0; i < randomWormLength; i++) {
                        int bodyType;
                        if (i % 2 == 0) {
                            bodyType = ModContent.NPCType<AoshunArms>();
                        }
                        else {
                            bodyType = ModContent.NPCType<AoshunBody>();
                        }
                        latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.position.X + NPC.width / 2, (int)NPC.position.Y + NPC.height / 2,
                            bodyType, NPC.whoAmI, 0, latestNPC);
                        Main.npc[latestNPC].realLife = NPC.whoAmI;
                        Main.npc[latestNPC].ai[3] = NPC.whoAmI;
                    }
                    // 尾部
                    latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.position.X + NPC.width / 2, (int)NPC.position.Y + NPC.height / 2,
                        ModContent.NPCType<AoshunTail>(), NPC.whoAmI, 0, latestNPC);
                    Main.npc[latestNPC].realLife = NPC.whoAmI;
                    Main.npc[latestNPC].ai[3] = NPC.whoAmI;

                    NPC.ai[0] = 1;
                    NPC.netUpdate = true;
                }
            }

            // 参考原型: 地形碰撞检测
            int minTilePosX = (int)(NPC.position.X / 16.0) - 1;
            int maxTilePosX = (int)((NPC.position.X + NPC.width) / 16.0) + 2;
            int minTilePosY = (int)(NPC.position.Y / 16.0) - 1;
            int maxTilePosY = (int)((NPC.position.Y + NPC.height) / 16.0) + 2;
            if (minTilePosX < 0) minTilePosX = 0;
            if (maxTilePosX > Main.maxTilesX) maxTilePosX = Main.maxTilesX;
            if (minTilePosY < 0) minTilePosY = 0;
            if (maxTilePosY > Main.maxTilesY) maxTilePosY = Main.maxTilesY;

            bool collision = false;
            for (int i = minTilePosX; i < maxTilePosX; ++i) {
                for (int j = minTilePosY; j < maxTilePosY; ++j) {
                    if (Main.tile[i, j] != null && (Main.tile[i, j].HasUnactuatedTile && (Main.tileSolid[Main.tile[i, j].TileType] ||
                        Main.tileSolidTop[Main.tile[i, j].TileType] && Main.tile[i, j].TileFrameY == 0) ||
                        Main.tile[i, j].LiquidAmount > 64)) {
                        Vector2 tilePos;
                        tilePos.X = i * 16;
                        tilePos.Y = j * 16;
                        if (NPC.position.X + NPC.width > tilePos.X && NPC.position.X < tilePos.X + 16.0 &&
                            NPC.position.Y + NPC.height > (double)tilePos.Y && NPC.position.Y < tilePos.Y + 16.0) {
                            collision = true;
                            if (Main.rand.NextBool(100) && Main.tile[i, j].HasUnactuatedTile)
                                WorldGen.KillTile(i, j, true, true, false);
                        }
                    }
                }
            }

            // 参考原型: 远距离冲锋标记
            if (Vector2.Distance(NPC.Center, player.Center) >= 500) {
                chargePlayer = true;
            }
            if (Vector2.Distance(NPC.Center, player.Center) <= 350) {
                chargePlayer = false;
            }

            // 蠕虫移动速度
            float speed = 18f;
            if (IsPhase2) speed = 25f;
            float acceleration = 0.6f;

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

            // 参考原型: 非碰撞时下坠式蠕虫AI
            if (!collision) {
                NPC.TargetClosest(true);
                NPC.velocity.Y += 0.11f;
                if (NPC.velocity.Y > speed)
                    NPC.velocity.Y = speed;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.4) {
                    if (NPC.velocity.X < 0.0)
                        NPC.velocity.X -= acceleration * 1.1f;
                    else
                        NPC.velocity.X += acceleration * 1.1f;
                }
                else if (NPC.velocity.Y == speed) {
                    if (NPC.velocity.X < dirX)
                        NPC.velocity.X += acceleration;
                    else if (NPC.velocity.X > dirX)
                        NPC.velocity.X -= acceleration;
                }
                else if (NPC.velocity.Y > 4.0) {
                    if (NPC.velocity.X < 0.0)
                        NPC.velocity.X += acceleration * 0.9f;
                    else
                        NPC.velocity.X -= acceleration * 0.9f;
                }
            }

            // 参考原型: 碰撞或冲锋时挖掘式蠕虫AI
            if (collision || chargePlayer) {
                if (NPC.soundDelay == 0) {
                    float num1 = length / 40f;
                    if (num1 < 10.0) num1 = 10f;
                    if (num1 > 20.0) num1 = 20f;
                    NPC.soundDelay = (int)num1;
                    SoundEngine.PlaySound(SoundID.WormDig, NPC.position);
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
            }

            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;

            // 参考原型: 朝向
            if (NPC.velocity.X < 0f)
                NPC.spriteDirection = 1;
            else
                NPC.spriteDirection = -1;

            if (collision) {
                if (NPC.localAI[0] != 1)
                    NPC.netUpdate = true;
                NPC.localAI[0] = 1f;
            }
            else {
                if (NPC.localAI[0] != 0.0)
                    NPC.netUpdate = true;
                NPC.localAI[0] = 0.0f;
            }

            if ((NPC.velocity.X > 0.0 && NPC.oldVelocity.X < 0.0 || NPC.velocity.X < 0.0 && NPC.oldVelocity.X > 0.0 ||
                 NPC.velocity.Y > 0.0 && NPC.oldVelocity.Y < 0.0 || NPC.velocity.Y < 0.0 && NPC.oldVelocity.Y > 0.0) && !NPC.justHit)
                NPC.netUpdate = true;

            return false;
        }

        #endregion

        #region 攻击逻辑

        private void Attack(NPC npc) {
            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 2;

            switch ((int)internalAI[1]) {
                case 0:
                    // 雷柱雨（多波次下落）
                    if (internalAI[0] == 320 || internalAI[0] == 340 || internalAI[0] == 360 || internalAI[0] == 380) {
                        int count = Main.expertMode ? 10 : 8;
                        if (IsPhase2) count += 4;
                        for (int i = 0; i < count; i++) {
                            AoshunAttacks.LightningRain(npc);
                        }
                        SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 0.7f }, npc.Center);
                    }
                    break;

                case 1:
                    // 前方雷球
                    if (internalAI[0] == 350 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            npc.Center.X, npc.Center.Y, npc.velocity.X * 2, npc.velocity.Y,
                            ModContent.ProjectileType<AoshunThunderball>(), damage, 3f, Main.myPlayer);
                    }
                    break;

                case 2:
                    // 扇形雷球散射
                    if (internalAI[0] == 350) {
                        int count = Main.expertMode ? 8 : 6;
                        if (IsPhase2) count += 4;
                        AoshunAttacks.ThunderBurst(npc, count);
                        SoundEngine.PlaySound(SoundID.Item67 with { Pitch = 0.2f }, npc.Center);
                    }
                    break;

                case 3:
                    // 螺旋雷球（持续释放旋转弹幕）
                    if (internalAI[0] >= 310 && internalAI[0] <= 390 && (int)internalAI[0] % 8 == 0) {
                        float spinAngle = (internalAI[0] - 310) * 0.15f;
                        int arms = IsPhase2 ? 5 : 3;
                        float spd = Main.expertMode ? 10f : 8f;
                        AoshunAttacks.SpiralThunder(npc, spinAngle, arms, spd);
                    }
                    break;

                case 4:
                    // 追踪雷球连射（3波）
                    if (internalAI[0] == 320 || internalAI[0] == 350 || internalAI[0] == 380) {
                        int count = Main.expertMode ? 6 : 4;
                        if (IsPhase2) count += 2;
                        AoshunAttacks.HomingThunderBurst(npc, count);
                        SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.2f }, npc.Center);
                    }
                    break;

                case 5:
                    // 雷电环 + 雷柱雨组合技
                    if (internalAI[0] == 330) {
                        int ringCount = IsPhase2 ? 24 : 16;
                        AoshunAttacks.ThunderRing(npc, ringCount, 5f);
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f, Volume = 1.2f }, npc.Center);
                    }
                    if (internalAI[0] == 360) {
                        int ringCount = IsPhase2 ? 24 : 16;
                        AoshunAttacks.ThunderRing(npc, ringCount, 7f);
                    }
                    if (internalAI[0] >= 340 && internalAI[0] <= 380 && (int)internalAI[0] % 10 == 0) {
                        AoshunAttacks.LightningStorm(npc, IsPhase2 ? 4 : 2);
                    }
                    break;

                case 6:
                    // 龙息雷锥连射（张嘴持续喷射）
                    if (internalAI[0] == 310) {
                        fireAttack = true;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                        SoundEngine.PlaySound(SoundID.NPCDeath60 with { Pitch = 0.3f, Volume = 1.2f }, npc.Center);
                    }
                    if (internalAI[0] >= 310 && internalAI[0] <= 390 && (int)internalAI[0] % 6 == 0) {
                        int count = Main.expertMode ? 4 : 2;
                        AoshunAttacks.BreathLightning(npc, count);
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
                    // 雷柱激光大招（二阶段专属）
                    if (internalAI[0] == 320) {
                        fireAttack = true;
                        attackTimer = 0;
                        attackFrame = 0;
                        attackCounter = 0;
                        AoshunAttacks.ThunderBeam(npc);
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

using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天庭巡卫金龙 - 月球领主后的蠕虫类Boss
    /// 继承BasicWorm以使用正确的蠕虫跟随系统
    /// 贴图朝向：右边向前（正方向）
    /// </summary>
    public abstract class CelestialDragons : BasicWorm
    {
        // 贴图尺寸常量
        protected const int HeadTextureWidth = 382;
        protected const int HeadTextureHeight = 256;
        protected const int BodyTextureWidth = 152;
        protected const int BodyTextureHeight = 92;
        protected const int TailTextureWidth = 412;
        protected const int TailTextureHeight = 124;

        // 体节覆盖比例（40%覆盖）
        protected const float SegmentOverlapRatio = 0.40f;

        /// <summary>
        /// 不使用SpriteDirection翻转，我们手动处理
        /// </summary>
        public override bool IsUseSpriteDirection => false;

        /// <summary>
        /// 目标玩家
        /// </summary>
        public Player Target
        {
            get
            {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.TrailingMode[NPC.type] = 3;
            NPCID.Sets.TrailCacheLength[NPC.type] = 10;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.damage = 180;
            NPC.defense = 80;
            NPC.lifeMax = 500000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 500000f;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            SummonMax = 50;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
        {
            scale = 1.5f;
            if (NPCWormType != WormType.Head)
                return false;
            return null;
        }

        public override void BossHeadRotation(ref float rotation)
        {
            rotation = NPC.velocity.ToRotation();
        }

        public override void AI()
        {
            base.AI();

            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
            {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            if (NPCWormType == WormType.Head)
            {
                HeadAI();
            }

            // 金色粒子效果
            if (!Main.dedServ && Main.rand.NextBool(12))
            {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height,
                    DustID.GoldFlame, NPC.velocity.X * 0.1f, NPC.velocity.Y * 0.1f, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(NPC.Center, 0.5f, 0.4f, 0.1f);
        }

        /// <summary>
        /// 重写位置计算 - 体节被头部牵引，不是插值跟随
        /// </summary>
        public override void ChangePos()
        {
            if (FatherNPC == null) return;

            // 计算跟随距离
            float segmentWidth = GetSegmentWidth();
            float parentWidth = GetParentSegmentWidth();
            float targetDistance = (parentWidth + segmentWidth) * 0.5f * (1f - SegmentOverlapRatio);

            // 直接定位到父节点后方，不使用插值
            Vector2 directionFromParent = (NPC.Center - FatherNPC.Center).SafeNormalize(Vector2.UnitX);
            NPC.Center = FatherNPC.Center + directionFromParent * targetDistance;

            // 速度指向父节点方向
            NPC.velocity = (FatherNPC.Center - NPC.Center).SafeNormalize(Vector2.Zero) * FatherNPC.velocity.Length();

            // 旋转朝向父节点（被牵引的感觉）
            NPC.rotation = (FatherNPC.Center - NPC.Center).ToRotation();
        }

        protected virtual float GetSegmentWidth()
        {
            return NPCWormType switch
            {
                WormType.Head => HeadTextureWidth * 0.5f,
                WormType.Body => BodyTextureWidth,
                WormType.Tail => TailTextureWidth * 0.4f,
                _ => BodyTextureWidth
            };
        }

        protected float GetParentSegmentWidth()
        {
            if (FatherNPC?.ModNPC is CelestialDragons parentDragon)
            {
                return parentDragon.GetSegmentWidth();
            }
            return HeadTextureWidth * 0.5f;
        }

        private void HeadAI()
        {
            Player player = Target;

            if (!player.active || player.dead)
            {
                NPC.TargetClosest();
                player = Target;
                if (!player.active || player.dead)
                {
                    NPC.velocity.Y -= 0.5f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            // 初始化
            if (NPC.localAI[3] == 0f)
            {
                NPC.localAI[3] = 1f;
                // 初始化巡航方向（从玩家左侧或右侧开始）
                NPC.ai[3] = Main.rand.NextBool() ? 1f : -1f;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = 0.3f }, NPC.Center);
            }

            NPC.localAI[0]++;

            float lifeRatio = (float)NPC.life / NPC.lifeMax;
            int attackPhase = 0;
            if (lifeRatio < 0.75f) attackPhase = 1;
            if (lifeRatio < 0.5f) attackPhase = 2;
            if (lifeRatio < 0.25f) attackPhase = 3;

            int currentMode = (int)NPC.ai[0];
            switch (currentMode)
            {
                case 0: // 大范围巡空
                    WideRangeCruise(player, attackPhase);
                    break;
                case 1: // 俯冲穿越
                    DiveThroughAttack(player, attackPhase);
                    break;
                case 2: // 剑气喷吐
                    SwordBreathAttack(player, attackPhase);
                    break;
                case 3: // 大圆环绕
                    LargeCircleAttack(player, attackPhase);
                    break;
                case 4: // 龙威法阵 + 叉状天雷
                    DragonAuthorityAttack(player, attackPhase);
                    break;
                case 5: // 全屏攻击
                    if (attackPhase >= 2)
                        FullScreenAttack(player, attackPhase);
                    else
                        NPC.ai[0] = 0;
                    break;
            }

            NPC.ai[1]++;

            // 全局零散叉状天雷 - 贯穿整场战斗（在非法阵攻击时触发）
            if (currentMode != 4 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int ambientLightningInterval = Math.Max(150, 300 - attackPhase * 50);
                if ((int)NPC.localAI[0] % ambientLightningInterval == 0)
                {
                    // 在玩家附近随机位置释放零散天雷
                    Vector2 lightningPos = player.Center + Main.rand.NextVector2Circular(600f, 400f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), lightningPos + new Vector2(0, -1800f), Vector2.Zero,
                        ModContent.ProjectileType<ForkedLightningWarning>(), NPC.damage / 5, 3f, Main.myPlayer, lightningPos.X, lightningPos.Y);
                }
            }

            float phaseDuration = 600 - attackPhase * 100;
            if (NPC.ai[1] > phaseDuration)
            {
                NPC.ai[0]++;
                int maxPhase = attackPhase >= 2 ? 6 : 5;
                if (NPC.ai[0] >= maxPhase)
                    NPC.ai[0] = 0;
                NPC.ai[1] = 0;
                NPC.ai[2] = 0;
                // 切换巡航方向
                NPC.ai[3] *= -1;
                NPC.netUpdate = true;
            }

            // 头部始终朝向速度方向
            NPC.rotation = NPC.velocity.ToRotation();
        }

        /// <summary>
        /// 确保蠕虫保持最小速度并使用宽转弯
        /// </summary>
        private void ApplyMovement(Vector2 targetPos, float baseSpeed, float turnRate, float minSpeed)
        {
            Vector2 toTarget = targetPos - NPC.Center;
            float distToTarget = toTarget.Length();
            
            // 计算期望速度方向
            Vector2 desiredDirection = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX));
            
            // 限制转向速率以避免身体交叉（宽转弯）
            float currentAngle = NPC.velocity.ToRotation();
            float targetAngle = desiredDirection.ToRotation();
            float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
            
            // 限制每帧最大转向角度
            float maxTurnPerFrame = turnRate;
            angleDiff = MathHelper.Clamp(angleDiff, -maxTurnPerFrame, maxTurnPerFrame);
            
            float newAngle = currentAngle + angleDiff;
            Vector2 newDirection = newAngle.ToRotationVector2();
            
            // 速度基于距离调整，但保持最小速度
            float targetSpeed = baseSpeed;
            if (distToTarget < 200f)
            {
                // 接近目标时不减速太多，保持流畅
                targetSpeed = Math.Max(minSpeed, baseSpeed * 0.8f);
            }
            
            // 确保最小速度
            float currentSpeed = NPC.velocity.Length();
            float newSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, 0.05f);
            newSpeed = Math.Max(newSpeed, minSpeed);
            
            NPC.velocity = newDirection * newSpeed;
        }

        /// <summary>
        /// 大范围巡空 - 使用航点系统完成完整的穿越，不会在玩家附近抖动
        /// </summary>
        private void WideRangeCruise(Player player, int phase)
        {
            float direction = NPC.ai[3]; // 1 或 -1
            
            // 使用localAI[1]存储当前航点阶段
            int waypointPhase = (int)NPC.localAI[1];
            
            // 定义航点：大范围的穿越路径
            float horizontalDist = 1400f + phase * 200f;
            float verticalRange = 500f + phase * 100f;
            float baseHeight = 350f;
            
            Vector2 targetPos;
            float reachDist = 250f; // 到达航点的判定距离
            
            switch (waypointPhase % 4)
            {
                case 0: // 飞到玩家一侧上方
                    targetPos = player.Center + new Vector2(direction * horizontalDist, -baseHeight - verticalRange);
                    break;
                case 1: // 穿越到另一侧下方
                    targetPos = player.Center + new Vector2(-direction * horizontalDist, -baseHeight + verticalRange * 0.5f);
                    break;
                case 2: // 飞到另一侧上方
                    targetPos = player.Center + new Vector2(-direction * horizontalDist, -baseHeight - verticalRange * 0.8f);
                    break;
                default: // 穿越回起始侧下方
                    targetPos = player.Center + new Vector2(direction * horizontalDist, -baseHeight + verticalRange * 0.3f);
                    break;
            }
            
            // 检查是否到达当前航点
            if (Vector2.Distance(NPC.Center, targetPos) < reachDist)
            {
                NPC.localAI[1]++;
            }
            
            // 使用宽转弯移动到目标
            float speed = 24f + phase * 4f;
            float minSpeed = 20f + phase * 3f;
            float turnRate = 0.025f; // 较慢的转向速率
            ApplyMovement(targetPos, speed, turnRate, minSpeed);

            // 发射预警（预警弹幕将要划过的范围）
            if ((int)NPC.ai[1] % 100 == 50 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 futurePos = NPC.Center + NPC.velocity * 50f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                    futurePos.X, futurePos.Y);
            }

            // 发射辐射弹幕
            if ((int)NPC.ai[1] % (70 - phase * 12) == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int count = 4 + phase;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 vel = angle.ToRotationVector2() * (6f + phase);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
                }
            }
        }

        /// <summary>
        /// 俯冲穿越 - 从高处俯冲穿过玩家位置，使用宽转弯
        /// </summary>
        private void DiveThroughAttack(Player player, int phase)
        {
            int subPhase = (int)NPC.ai[2];
            float side = NPC.ai[3];

            if (subPhase == 0) // 飞到一侧高处（准备阶段）
            {
                Vector2 targetPos = player.Center + new Vector2(side * 1200f, -800f);
                
                float speed = 28f + phase * 3f;
                float minSpeed = 22f + phase * 2f;
                ApplyMovement(targetPos, speed, 0.03f, minSpeed);

                if (Vector2.Distance(NPC.Center, targetPos) < 200f)
                {
                    NPC.ai[2] = 1;
                    // 发出预警
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 diveTarget = player.Center + new Vector2(-side * 1000f, 300f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                            diveTarget.X, diveTarget.Y);
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = 0.5f }, NPC.Center);
                }
            }
            else if (subPhase == 1) // 俯冲穿越（高速阶段）
            {
                Vector2 targetPos = player.Center + new Vector2(-side * 1000f, 300f);
                
                // 俯冲时使用高速但限制转向
                float speed = 38f + phase * 5f;
                float minSpeed = 32f + phase * 4f;
                ApplyMovement(targetPos, speed, 0.02f, minSpeed); // 更小的转向速率

                // 俯冲时喷射剑气
                if ((int)NPC.ai[1] % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 sideDir = NPC.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, sideDir * 10f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -sideDir * 10f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                }

                // 检查是否完成穿越（到达目标点或已经过了玩家下方足够远）
                if (Vector2.Distance(NPC.Center, targetPos) < 200f || 
                    (NPC.Center.Y > player.Center.Y + 400f && MathF.Abs(NPC.Center.X - player.Center.X) > 600f))
                {
                    NPC.ai[2] = 2;
                }
            }
            else // 回升阶段（宽转弯回到上方）
            {
                float oppositeSide = -side;
                Vector2 targetPos = player.Center + new Vector2(oppositeSide * 800f, -500f);
                
                float speed = 22f + phase * 2f;
                float minSpeed = 18f + phase * 2f;
                ApplyMovement(targetPos, speed, 0.025f, minSpeed);
            }
        }

        /// <summary>
        /// 剑气喷吐 - 使用航点系统的8字形移动
        /// </summary>
        private void SwordBreathAttack(Player player, int phase)
        {
            float direction = NPC.ai[3];
            
            // 使用localAI[2]存储8字形航点阶段
            int waypointPhase = (int)NPC.localAI[2];
            
            // 8字形航点
            float horizontalDist = 900f + phase * 100f;
            float verticalDist = 400f + phase * 50f;
            float baseHeight = 300f;
            
            Vector2 targetPos;
            
            switch (waypointPhase % 4)
            {
                case 0: // 右上
                    targetPos = player.Center + new Vector2(direction * horizontalDist, -baseHeight - verticalDist);
                    break;
                case 1: // 左下（穿过中心）
                    targetPos = player.Center + new Vector2(-direction * horizontalDist * 0.5f, -baseHeight + verticalDist * 0.3f);
                    break;
                case 2: // 左上
                    targetPos = player.Center + new Vector2(-direction * horizontalDist, -baseHeight - verticalDist * 0.8f);
                    break;
                default: // 右下（穿过中心）
                    targetPos = player.Center + new Vector2(direction * horizontalDist * 0.5f, -baseHeight + verticalDist * 0.5f);
                    break;
            }
            
            // 检查是否到达当前航点
            if (Vector2.Distance(NPC.Center, targetPos) < 200f)
            {
                NPC.localAI[2]++;
            }

            float speed = 22f + phase * 3f;
            float minSpeed = 18f + phase * 2f;
            ApplyMovement(targetPos, speed, 0.028f, minSpeed);

            // 喷吐剑气
            int interval = Math.Max(12, 35 - phase * 7);
            if ((int)NPC.ai[1] % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 toPlayerNorm = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                int projectileCount = 3 + phase;
                float spread = 0.5f;

                for (int i = 0; i < projectileCount; i++)
                {
                    float angle = spread * ((i - (projectileCount - 1) / 2f) / (projectileCount - 1));
                    Vector2 velocity = toPlayerNorm.RotatedBy(angle) * 14f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toPlayerNorm * 50, velocity,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                }

                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, NPC.Center);
            }
        }

        /// <summary>
        /// 大圆环绕 - 在玩家周围画大圆，保持稳定速度
        /// </summary>
        private void LargeCircleAttack(Player player, int phase)
        {
            float radius = 800f - phase * 50f;
            float angularSpeed = 0.018f + phase * 0.004f;
            float angle = NPC.ai[1] * angularSpeed * NPC.ai[3];

            // 使用椭圆轨道，略高于玩家
            Vector2 targetPos = player.Center + new Vector2(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius * 0.6f - 150f
            );

            float speed = 22f + phase * 4f;
            float minSpeed = 18f + phase * 3f;
            ApplyMovement(targetPos, speed, 0.035f, minSpeed);

            // 发射辐射能量弹
            int interval = Math.Max(8, 25 - phase * 4);
            if ((int)NPC.ai[1] % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 toPlayerNorm = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPlayerNorm * 9f,
                    ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
            }
        }

        /// <summary>
        /// 龙威法阵攻击 - 在玩家周围召唤龙威法阵，降下叉状天雷（集中释放）
        /// </summary>
        private void DragonAuthorityAttack(Player player, int phase)
        {
            float direction = NPC.ai[3];
            
            // 使用航点系统盘旋
            int waypointPhase = (int)NPC.localAI[2] % 4;
            float radius = 700f - phase * 40f;
            float baseHeight = 450f;
            
            Vector2 targetPos = waypointPhase switch
            {
                0 => player.Center + new Vector2(direction * radius, -baseHeight),
                1 => player.Center + new Vector2(0, -baseHeight - 200f),
                2 => player.Center + new Vector2(-direction * radius, -baseHeight),
                _ => player.Center + new Vector2(0, -baseHeight + 100f)
            };
            
            if (Vector2.Distance(NPC.Center, targetPos) < 180f)
            {
                NPC.localAI[2]++;
            }

            float speed = 18f + phase * 3f;
            float minSpeed = 15f + phase * 2f;
            ApplyMovement(targetPos, speed, 0.03f, minSpeed);

            // 蓄力特效
            if (!Main.dedServ && NPC.ai[1] < 60 && Main.rand.NextBool(2))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * Main.rand.NextFloat(40, 80);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
            }

            // 召唤龙威法阵预警
            if ((int)NPC.ai[1] == 60)
            {
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.2f, Volume = 1f }, player.Center);
            }

            // 在玩家位置召唤法阵预警
            if ((int)NPC.ai[1] == 70 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Projectile.NewProjectile(NPC.GetSource_FromAI(), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<DragonCircleWarning>(), NPC.damage / 3, 0f, Main.myPlayer, NPC.damage / 3, phase);
            }

            // 后续追加法阵
            if (phase >= 1 && (int)NPC.ai[1] == 150 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 leftPos = player.Center + new Vector2(-450f, 0);
                Vector2 rightPos = player.Center + new Vector2(450f, 0);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), leftPos, Vector2.Zero,
                    ModContent.ProjectileType<DragonCircleWarning>(), NPC.damage / 3, 0f, Main.myPlayer, NPC.damage / 3, phase);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), rightPos, Vector2.Zero,
                    ModContent.ProjectileType<DragonCircleWarning>(), NPC.damage / 3, 0f, Main.myPlayer, NPC.damage / 3, phase);
            }

            if (phase >= 2 && (int)NPC.ai[1] == 230 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * i;
                    Vector2 circlePos = player.Center + angle.ToRotationVector2() * 400f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), circlePos, Vector2.Zero,
                        ModContent.ProjectileType<DragonCircleWarning>(), NPC.damage / 4, 0f, Main.myPlayer, NPC.damage / 4, phase);
                }
            }

            // 集中释放叉状天雷（在法阵攻击期间频率更高）
            int lightningInterval = Math.Max(30, 60 - phase * 12);
            if ((int)NPC.ai[1] > 80 && (int)NPC.ai[1] % lightningInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                // 预测玩家位置
                Vector2 predictedPos = player.Center + player.velocity * 25f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), predictedPos + new Vector2(0, -1800f), Vector2.Zero,
                    ModContent.ProjectileType<ForkedLightningWarning>(), NPC.damage / 4, 5f, Main.myPlayer, predictedPos.X, predictedPos.Y);

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.5f }, predictedPos);
            }

            // 持续发射辐射弹幕
            if ((int)NPC.ai[1] % (50 - phase * 8) == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int count = 3 + phase;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathHelper.TwoPi * i / count + NPC.ai[1] * 0.02f;
                    Vector2 vel = angle.ToRotationVector2() * (5f + phase);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 5, 2f, Main.myPlayer);
                }
            }
        }

        /// <summary>
        /// 全屏攻击 - 使用航点系统在移动中释放
        /// </summary>
        private void FullScreenAttack(Player player, int phase)
        {
            float direction = NPC.ai[3];
            
            // 使用航点系统保持移动
            int waypointPhase = (int)NPC.localAI[2] % 4;
            float radius = 650f;
            float baseHeight = 500f;
            
            Vector2 targetPos = waypointPhase switch
            {
                0 => player.Center + new Vector2(direction * radius, -baseHeight),
                1 => player.Center + new Vector2(0, -baseHeight - 150f),
                2 => player.Center + new Vector2(-direction * radius, -baseHeight),
                _ => player.Center + new Vector2(0, -baseHeight + 50f)
            };
            
            if (Vector2.Distance(NPC.Center, targetPos) < 180f)
            {
                NPC.localAI[2]++;
            }

            float speed = 18f + phase * 2f;
            float minSpeed = 14f + phase * 2f;
            ApplyMovement(targetPos, speed, 0.025f, minSpeed);

            // 蓄力特效
            if (!Main.dedServ && NPC.ai[1] < 90 && Main.rand.NextBool(2))
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dust = Dust.NewDust(NPC.Center + vel * 50, 0, 0, DustID.GoldFlame, -vel.X * 2, -vel.Y * 2, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 释放攻击
            if ((int)NPC.ai[1] == 90)
            {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.3f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 30);
            }

            // 环形闪电
            if ((int)NPC.ai[1] == 100 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int count = 12 + phase * 3;
                for (int i = 0; i < count; i++)
                {
                    float ang = MathHelper.TwoPi * i / count;
                    Vector2 spawnPos = player.Center + ang.ToRotationVector2() * 800;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                        ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                        player.Center.X, player.Center.Y);
                }
            }

            if ((int)NPC.ai[1] == 140 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int count = 12 + phase * 3;
                for (int i = 0; i < count; i++)
                {
                    float ang = MathHelper.TwoPi * i / count;
                    Vector2 spawnPos = player.Center + ang.ToRotationVector2() * 800;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, -ang.ToRotationVector2() * 12f,
                        ModContent.ProjectileType<CelestialLightning>(), NPC.damage / 3, 5f, Main.myPlayer);
                }
            }

            // 天降金剑
            if (NPC.ai[1] >= 120 && (int)NPC.ai[1] % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 spawnPos = player.Center + new Vector2(Main.rand.NextFloat(-800, 800), -700);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(Main.rand.NextFloat(-1, 1), 16f),
                    ModContent.ProjectileType<FallingSword>(), NPC.damage / 3, 3f, Main.myPlayer);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            // 贴图向右为正方向
            // rotation = 速度方向角度
            // 当速度向右(0度)时不需要额外旋转
            // 当速度向左(180度)时需要垂直翻转
            Vector2 origin = texture.Size() / 2; // 右边中心为原点（头部前端）
            SpriteEffects effects = SpriteEffects.None;

            // 如果速度向左，垂直翻转贴图
            if (NPC.velocity.X < 0)
            {
                effects = SpriteEffects.FlipVertically;
            }

            // 绘制发光层
            Color glowColor = Color.Gold * 0.35f;
            glowColor.A = 0;
            spriteBatch.Draw(
                texture,
                NPC.Center - screenPos,
                null,
                glowColor,
                NPC.rotation,
                origin,
                NPC.scale * 1.08f,
                effects,
                0f
            );

            // 绘制本体
            spriteBatch.Draw(
                texture,
                NPC.Center - screenPos,
                null,
                drawColor,
                NPC.rotation,
                origin,
                NPC.scale,
                effects,
                0f
            );

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPCWormType == WormType.Head && !Main.dedServ)
            {
                Texture2D sparkTex = ACMAsset.Sparkle;
                if (sparkTex != null)
                {
                    Color sparkColor = Color.Gold;
                    sparkColor.A = 0;

                    float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.15f;

                    spriteBatch.Draw(
                        sparkTex,
                        NPC.Center - screenPos,
                        null,
                        sparkColor * 0.2f,
                        Main.GlobalTimeWrappedHourly * 0.5f,
                        sparkTex.Size() / 2f,
                        NPC.scale * 0.35f * pulseScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (!Main.dedServ)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (NPC.life <= 0 && NPCWormType == WormType.Head)
            {
                if (!Main.dedServ)
                {
                    for (int i = 0; i < 80; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(12, 12);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);
            }
        }

        public override bool CheckActive() => false;

        public override void BossLoot(ref string name, ref int potionType)
        {
            potionType = ItemID.SuperHealingPotion;
        }
    }
}

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
                case 4: // 全屏攻击
                    if (attackPhase >= 2)
                        FullScreenAttack(player, attackPhase);
                    else
                        NPC.ai[0] = 0;
                    break;
            }

            NPC.ai[1]++;

            float phaseDuration = 600 - attackPhase * 100;
            if (NPC.ai[1] > phaseDuration)
            {
                NPC.ai[0]++;
                int maxPhase = attackPhase >= 2 ? 5 : 4;
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
        /// 大范围巡空 - 从屏幕一侧穿越到另一侧，大起大落
        /// </summary>
        private void WideRangeCruise(Player player, int phase)
        {
            float direction = NPC.ai[3]; // 1 或 -1
            float time = NPC.ai[1] * 0.008f; // 较慢的周期

            // 大范围水平移动：屏幕宽度的2倍范围
            float horizontalRange = 1200f + phase * 200f;
            // 大范围垂直移动：大起大落
            float verticalRange = 600f + phase * 100f;
            float baseHeight = 400f;

            // 正弦波轨迹：水平匀速移动，垂直大幅波动
            float targetX = player.Center.X + direction * horizontalRange * MathF.Cos(time);
            float targetY = player.Center.Y - baseHeight + MathF.Sin(time * 2f) * verticalRange;

            Vector2 targetPos = new Vector2(targetX, targetY);
            Vector2 toTarget = targetPos - NPC.Center;

            // 高速巡航
            float speed = 22f + phase * 4f;
            Vector2 desiredVelocity = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;

            // 平滑转向但保持高速
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.03f);

            // 确保最小速度
            float minSpeed = 18f + phase * 2f;
            if (NPC.velocity.Length() < minSpeed)
            {
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitX * direction) * minSpeed;
            }

            // 发射预警闪电（预警弹幕将要划过的范围）
            if ((int)NPC.ai[1] % 120 == 60 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                // 计算龙即将穿越的路径
                Vector2 futurePos = NPC.Center + NPC.velocity * 60f; // 预测1秒后的位置
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                    futurePos.X, futurePos.Y);
            }

            // 发射辐射弹幕
            if ((int)NPC.ai[1] % (80 - phase * 15) == 0 && Main.netMode != NetmodeID.MultiplayerClient)
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
        /// 俯冲穿越 - 从高处俯冲穿过玩家位置
        /// </summary>
        private void DiveThroughAttack(Player player, int phase)
        {
            int subPhase = (int)NPC.ai[2];

            if (subPhase == 0) // 飞到一侧高处
            {
                float side = NPC.ai[3];
                Vector2 targetPos = player.Center + new Vector2(side * 1000f, -700f);
                Vector2 toTarget = targetPos - NPC.Center;

                float speed = 25f + phase * 3f;
                Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
                NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.05f);

                if (toTarget.Length() < 150f)
                {
                    NPC.ai[2] = 1;
                    // 发出预警
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 diveTarget = player.Center + new Vector2(-side * 800f, 200f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                            diveTarget.X, diveTarget.Y);
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = 0.5f }, NPC.Center);
                }
            }
            else if (subPhase == 1) // 俯冲穿越
            {
                float side = NPC.ai[3];
                Vector2 targetPos = player.Center + new Vector2(-side * 800f, 200f);
                Vector2 toTarget = targetPos - NPC.Center;

                float speed = 35f + phase * 5f;
                Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
                NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.08f);

                // 俯冲时喷射剑气
                if ((int)NPC.ai[1] % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 sideDir = NPC.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, sideDir * 8f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -sideDir * 8f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                }

                if (toTarget.Length() < 150f || NPC.Center.Y > player.Center.Y + 300f)
                {
                    NPC.ai[2] = 2;
                }
            }
            else // 回升
            {
                float side = -NPC.ai[3];
                Vector2 targetPos = player.Center + new Vector2(side * 600f, -400f);
                Vector2 toTarget = targetPos - NPC.Center;

                float speed = 20f + phase * 2f;
                Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
                NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.04f);
            }
        }

        /// <summary>
        /// 剑气喷吐 - 边巡航边喷吐
        /// </summary>
        private void SwordBreathAttack(Player player, int phase)
        {
            float direction = NPC.ai[3];
            float time = NPC.ai[1] * 0.015f;

            // 大范围水平8字形移动
            float horizontalRange = 800f;
            float verticalRange = 300f;

            float targetX = player.Center.X + MathF.Sin(time) * horizontalRange * direction;
            float targetY = player.Center.Y - 300f + MathF.Sin(time * 2f) * verticalRange;

            Vector2 targetPos = new Vector2(targetX, targetY);
            Vector2 toTarget = targetPos - NPC.Center;

            float speed = 18f + phase * 3f;
            Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.04f);

            // 确保最小速度
            if (NPC.velocity.Length() < 14f)
            {
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitX * direction) * 14f;
            }

            // 喷吐剑气
            int interval = Math.Max(15, 40 - phase * 8);
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
        /// 大圆环绕 - 在玩家周围画大圆
        /// </summary>
        private void LargeCircleAttack(Player player, int phase)
        {
            float radius = 700f - phase * 50f;
            float angularSpeed = 0.02f + phase * 0.005f;
            float angle = NPC.ai[1] * angularSpeed * NPC.ai[3];

            Vector2 targetPos = player.Center + new Vector2(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius * 0.7f - 100f // 椭圆轨迹，略高于玩家
            );

            Vector2 toTarget = targetPos - NPC.Center;
            float speed = 20f + phase * 4f;
            Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.06f);

            // 发射辐射能量弹
            int interval = Math.Max(10, 30 - phase * 5);
            if ((int)NPC.ai[1] % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 toPlayerNorm = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPlayerNorm * 8f,
                    ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
            }
        }

        /// <summary>
        /// 全屏攻击 - 在移动中释放
        /// </summary>
        private void FullScreenAttack(Player player, int phase)
        {
            float direction = NPC.ai[3];
            float time = NPC.ai[1] * 0.01f;

            // 继续大范围移动
            float targetX = player.Center.X + MathF.Sin(time) * 600f * direction;
            float targetY = player.Center.Y - 500f + MathF.Cos(time * 1.5f) * 200f;

            Vector2 targetPos = new Vector2(targetX, targetY);
            Vector2 toTarget = targetPos - NPC.Center;

            float speed = 15f + phase * 2f;
            Vector2 desiredVel = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX)) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.04f);

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
                    // 预警路径
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
            Vector2 origin = new Vector2(texture.Width, texture.Height / 2f); // 右边中心为原点（头部前端）
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

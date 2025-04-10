using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System;

namespace AncientChineseMythology.NPCs.Monsters
{
    public class Demon : ModNPC
    {
        // 动画状态枚举（主要用于绘制）
        private enum AnimationState
        {
            Attack,
            Die,
            Run,
            Hurt,
            Idle
        }

        // 动画控制变量
        private float animationCounter = 0f;
        private int frameDuration = 6;
        private int frameTimer = 0;          // 辅助计时（用于死亡动画）
        private int currentFrame = 0;        // 用于记录死亡动画当前帧（PreDraw直接使用 dieTimer）

        // 各动画帧数
        private int attackFrameCount = 6;
        private int dieFrameCount = 10;
        private int runFrameCount = 4;
        private int hurtFrameCount = 3;
        private int idleFrameCount = 4;

        // 攻击控制变量
        private int attackCooldown = 0;
        private int attackAnimTimer = 0;
        private bool didDamageThisAttack = false;
        private int attackTriggerFrame = 3;  // 当攻击动画播放到此帧时发射投射物
        private int projectilesFired = 0;
        private Vector2 attackOffset = new Vector2(0, -5);

        // 不可打断的攻击标记及持续时长
        private bool isAttacking = false;
        private int fullAttackDuration = (3 * 30) + 30; // 约 120 帧

        // 状态标记
        private bool isDying = false;
        private int dieTimer = 0;
        private int hurtTimer = 0;

        // 额外击退效果
        private Vector2 extraKnockbackForce = Vector2.Zero;

        // 移动与攻击参数
        private float flySpeed = 5f;
        // 远程攻击条件：目标距离在200～400之间触发
        private float minRange = 200f;
        private float maxRange = 400f;

        // 位置卡住检测
        private int stuckCounter = 0;

        // invincibleTimer 处理：记录失敌/被阻挡时的计时，并记录初始速度
        private int invincibleTimer = 0;
        private Vector2 initialVelocity = Vector2.Zero;

        // 贴图资源
        private Texture2D attackTexture;
        private Texture2D dieTexture;
        private Texture2D runTexture;
        private Texture2D hurtTexture;
        private Texture2D idleTexture;

        // 伪路径，防止 tModLoader 自动加载单张贴图
        public override string Texture => "AncientChineseMythology/Textures/Demon/idle_01";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            // 加载各状态动画贴图
            attackTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Demon/Attack").Value;
            dieTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Demon/Die").Value;
            runTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Demon/Run").Value;
            hurtTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Demon/Hurt").Value;
            idleTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Demon/Idle").Value;

            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 20;
            NPC.defense = 8;
            NPC.lifeMax = 100;
            NPC.knockBackResist = 0.5f;
            NPC.value = 100f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            // 能飞行，但与实体碰撞；平台穿越逻辑在 AI 中控制
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.aiStyle = -1;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            return !Main.dayTime ? 0.4f : 0f;
        }

        /// <summary>
        /// 根据当前条件返回动画状态（主要用于绘制）
        /// 攻击状态：当目标距离小于70 或处于 [minRange, maxRange] 内且正在攻击
        /// Idle：当目标处于理想停留状态
        /// Run：其它情况
        /// Die：死亡动画
        /// Hurt：受伤状态
        /// </summary>
        private AnimationState GetAnimationState()
        {
            if (isDying)
                return AnimationState.Die;
            if (hurtTimer > 0)
                return AnimationState.Hurt;
            if (isAttacking)
                return AnimationState.Attack;

            Player target = Main.player[NPC.target];
            float dist = Vector2.Distance(NPC.Center, target.Center);
            if (dist < 70f)
                return AnimationState.Attack;
            if (dist >= minRange && dist <= maxRange)
                return AnimationState.Idle;
            return AnimationState.Run;
        }

        public override void AI()
        {
            // 目标检测及朝向设置
            NPC.TargetClosest(true);
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers)
            {
                NPC.velocity = Vector2.Zero;
                return;
            }
            Player target = Main.player[NPC.target];

            // 始终使 NPC 面向玩家
            NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;

            // 玩家死亡时淡出处理
            if (target.dead)
            {
                NPC.velocity = Vector2.Zero;
                NPC.alpha += 5;
                if (NPC.alpha >= 255)
                    NPC.active = false;
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.timeLeft = 300;

            //  死亡与受伤处理
            if (isDying)
            {
                NPC.damage = 0;
                NPC.velocity = Vector2.Zero;
                dieTimer++;
                // 使用 dieTimer 来计算死亡动画帧（PreDraw中直接使用 dieTimer/frameDuration）
                if (dieTimer > dieFrameCount * frameDuration + 10)
                {
                    NPC.NPCLoot();
                    NPC.active = false;
                }
                return;
            }
            if (hurtTimer > 0)
            {
                NPC.velocity = Vector2.Zero;
            }
            else
            {
                // 非攻击状态下的追击逻辑
                if (!isAttacking)
                {
                    Vector2 toPlayer = target.Center - NPC.Center;
                    float dist = toPlayer.Length();
                    if (toPlayer != Vector2.Zero)
                        toPlayer.Normalize();

                    if (dist < minRange)
                        NPC.velocity = -toPlayer * flySpeed;  // 目标过近则后退
                    else if (dist > maxRange)
                        NPC.velocity = toPlayer * flySpeed;   // 目标过远则追近
                    else
                        NPC.velocity = Vector2.Zero;          // 理想范围内停留

                    // 当目标处于 [minRange, maxRange] 内且攻击冷却结束时，开始攻击
                    if (attackCooldown <= 0 && dist >= minRange && dist <= maxRange)
                        StartAttack();
                }
                else
                {
                    // 攻击期间保持静止，不受目标移动干扰
                    NPC.velocity = Vector2.Zero;
                }

                // 攻击动画处理及远程投射
                if (isAttacking)
                {
                    attackAnimTimer++;
                    // 每隔 (projectilesFired+1)*30 帧重置 didDamageThisAttack，允许多次发射
                    if (attackAnimTimer >= (projectilesFired + 1) * 30)
                        didDamageThisAttack = false;

                    if (!didDamageThisAttack && (attackAnimTimer / frameDuration) % attackFrameCount == attackTriggerFrame)
                    {
                        FireProjectile(target);
                    }
                    if (attackAnimTimer >= fullAttackDuration)
                    {
                        attackCooldown = 90;
                        attackAnimTimer = 0;
                        projectilesFired = 0;
                        isAttacking = false;
                    }
                }
            }

            // 攻击冷却递减
            if (attackCooldown > 0)
                attackCooldown--;

            // 平台检测及穿越属性设置
            bool onPlatform = false;
            for (int i = (int)(NPC.Bottom.X / 16); i <= (int)((NPC.Bottom.X + NPC.width) / 16); i++)
            {
                for (int j = (int)(NPC.Bottom.Y / 16); j <= (int)((NPC.Bottom.Y + 1) / 16); j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (tile != null && tile.HasTile && Main.tileSolidTop[tile.TileType])
                    {
                        onPlatform = true;
                        break;
                    }
                }
                if (onPlatform)
                    break;
            }
            // 若在平台上且正处于下落状态，则允许穿越平台
            if (onPlatform && NPC.velocity.Y > 0)
                NPC.noTileCollide = true;
            else
                NPC.noTileCollide = false;

            // invincibleTimer 处理
            if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, target.position, target.width, target.height))
            {
                invincibleTimer++;
                if (invincibleTimer == 120)
                {
                    initialVelocity = -NPC.velocity;
                    if (initialVelocity != Vector2.Zero)
                        initialVelocity.Normalize();
                }
                if (invincibleTimer > 120)
                {
                    NPC.velocity = initialVelocity;
                    // 此处设置 spriteDirection 根据初始反向速度，而不是始终面向目标
                    NPC.spriteDirection = initialVelocity.X > 0 ? 1 : -1;
                }
            }
            else
            {
                invincibleTimer = 0;
            }

            // 额外击退效果叠加与衰减
            NPC.velocity += extraKnockbackForce;
            extraKnockbackForce *= 0.9f;

            // TileCollision 处理
            bool fallThrough = NPC.velocity.Y > 0; // 向下运动允许穿越平台
            Vector2 startPosition = NPC.position;
            NPC.velocity = Collision.TileCollision(NPC.position, NPC.velocity, NPC.width, NPC.height, fallThrough);

            // 位置卡住检测
            float distanceMoved = Vector2.DistanceSquared(NPC.position, startPosition);
            if (distanceMoved < 1f)
                stuckCounter++;
            else
                stuckCounter = 0;
            if (stuckCounter > 30)
            {
                float currentDist = Vector2.Distance(NPC.Center, target.Center);
                bool canAttack = (!isAttacking && attackCooldown <= 0 && currentDist <= maxRange);
                if (currentDist < minRange && canAttack)
                {
                    StartAttack();
                }
                else
                {
                    float verticalDir = Main.rand.NextBool() ? -1f : 1f;
                    float horizontalOffset = Main.rand.NextFloat(-0.5f, 0.5f);
                    Vector2 newDir = new Vector2(horizontalOffset, verticalDir).SafeNormalize(Vector2.UnitY);
                    NPC.velocity = newDir * flySpeed;
                }
                stuckCounter = 0;
            }

            if (hurtTimer > 0)
                hurtTimer--;

            // 更新动画计时器
            animationCounter += 1f;

            // 始终保持 NPC 面向玩家
            // 如果 invincibleTimer 未触发则保持面向目标，否则由 invincibleTimer 处理 spriteDirection
            if (invincibleTimer <= 120)
                NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;
        }

        #region 攻击、死亡及辅助方法

        private void StartAttack()
        {
            isAttacking = true;
            attackAnimTimer = 0;
            didDamageThisAttack = false;
            projectilesFired = 0;
        }

        private void FireProjectile(Player target)
        {
            int projectileType = ModContent.ProjectileType<Projectiles.Demon_Proj>();
            Vector2 spawnPos = NPC.Center + attackOffset;
            Vector2 projVel = target.Center - (NPC.Center + attackOffset);
            if (projVel != Vector2.Zero)
                projVel.Normalize();
            projVel *= 8f;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, projVel, projectileType, NPC.damage, 0f, Main.myPlayer);
            projectilesFired++;
            didDamageThisAttack = true;
        }

        private void Despawn()
        {
            NPC.active = false;
        }

        #endregion

        #region 受击与绘制

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (!isDying && NPC.life <= 0)
            {
                isDying = true;
                NPC.life = 1; // 确保 NPC 不会被重复击杀
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.netUpdate = true;
            }
            else
            {
                hurtTimer = 20;
            }
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            ApplyKnockback(hit);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            ApplyKnockback(hit);
        }

        private void ApplyKnockback(NPC.HitInfo hit)
        {
            float factor = 0.3f;
            Vector2 extraForce = new Vector2(hit.Knockback * factor * hit.HitDirection,
                                               -hit.Knockback * factor * 0.1f);
            extraKnockbackForce += extraForce;
            hurtTimer = 20;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            AnimationState state = GetCurrentAnimationState();
            Texture2D texture;
            int totalFrames = 1;
            switch (state)
            {
                case AnimationState.Attack:
                    texture = attackTexture;
                    totalFrames = attackFrameCount;
                    break;
                case AnimationState.Die:
                    texture = dieTexture;
                    totalFrames = dieFrameCount;
                    break;
                case AnimationState.Run:
                    texture = runTexture;
                    totalFrames = runFrameCount;
                    break;
                case AnimationState.Hurt:
                    texture = hurtTexture;
                    totalFrames = hurtFrameCount;
                    break;
                default:
                    texture = idleTexture;
                    totalFrames = idleFrameCount;
                    break;
            }

            int frameHeight = texture.Height / totalFrames;
            int currentAnimFrame;
            if (isDying)
                // 使用 dieTimer 来计算当前死亡动画帧
                currentAnimFrame = Math.Min((int)(dieTimer / (float)frameDuration), totalFrames - 1);
            else if (state == AnimationState.Attack)
                currentAnimFrame = (attackAnimTimer / frameDuration) % totalFrames;
            else
                currentAnimFrame = (int)(animationCounter / frameDuration) % totalFrames;
            Rectangle sourceRect = new Rectangle(0, currentAnimFrame * frameHeight, texture.Width, frameHeight);
            Vector2 currentOffset = (state == AnimationState.Attack) ? attackOffset : Vector2.Zero;
            Vector2 drawPosNPC = (NPC.Center + currentOffset) - screenPos;
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
            SpriteEffects effects = (NPC.spriteDirection == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float scale = 0.8f;
            spriteBatch.Draw(texture, drawPosNPC, sourceRect, drawColor * NPC.Opacity, NPC.rotation, origin, scale, effects, 0f);
            return false;
        }

        // 根据当前情况返回绘制用的动画状态
        private AnimationState GetCurrentAnimationState()
        {
            if (isDying)
                return AnimationState.Die;
            if (hurtTimer > 0)
                return AnimationState.Hurt;
            if (isAttacking)
                return AnimationState.Attack;
            return (NPC.velocity.LengthSquared() > 0.01f) ? AnimationState.Run : AnimationState.Idle;
        }

        #endregion
    }
}

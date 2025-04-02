using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace AncientChineseMythology.NPCs.Monsters
{
    public class JiaoSha : ModNPC
    {
        // 七种动画的帧数定义
        private const int AttackFrameCount = 6;
        private const int DieFrameCount = 8;
        private const int HurtFrameCount = 3;
        private const int IdleFrameCount = 4;
        private const int JumpFrameCount = 4;
        private const int RunFrameCount = 6;

        // 每帧持续时间（单位 tick）
        private int frameDuration = 6;

        // 状态枚举
        private enum JiaoShaState
        {
            Idle,
            Run,
            Jump,
            Attack,
            Hurt,
            Die
        }

        // 当前状态作为私有字段
        private JiaoShaState state = JiaoShaState.Idle;

        // 动画计时与当前帧
        private float animationCounter = 0f;
        private int frameTimer = 0;
        private int currentFrame = 0;

        // 贴图资源
        private Texture2D attackTexture;
        private Texture2D dieTexture;
        private Texture2D hurtTexture;
        private Texture2D idleTexture;
        private Texture2D jumpTexture;
        private Texture2D runTexture;

        // 状态控制
        private bool dying = false;
        private bool isDead = false;
        private int dieTimer = 0;
        private int deathIdleTimer = 0;
        private int hurtTimer = 0;

        // 攻击冷却与动画控制
        private int attackCooldown = 0;
        private int jumpCooldown = 0;
        private int attackAnimTimer = 0;
        private bool didDamageThisAttack = false;

        // 运动参数
        private float runSpeed = 2.5f;
        private float attackRange = 80f;    // 普通攻击判定距离
        private float chaseRange = 500f;    // 追击范围
        private float gravity = 0.3f;

        private bool onGround = false;

        // 额外击退力（受击时叠加）
        private Vector2 extraKnockbackForce = Vector2.Zero;

        // invincibleTimer 处理：记录失敌/被阻挡时的计时，并记录初始速度
        private int invincibleTimer = 0;
        private Vector2 initialVelocity = Vector2.Zero;

        // 强制使用假的 Texture 路径（防止自动加载单张贴图）
        public override string Texture => "AncientChineseMythology/Textures/JiaoSha/idle_1";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            // 加载各动画贴图
            attackTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/attack").Value;
            dieTexture    = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/die").Value;
            hurtTexture   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/hurt").Value;
            idleTexture   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/idle").Value;
            jumpTexture   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/jump").Value;
            runTexture    = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/JiaoSha/run").Value;

            // NPC 基本属性
            NPC.width = 40;
            NPC.height = 30;
            NPC.damage = 25;
            NPC.defense = 8;
            NPC.lifeMax = 100;
            NPC.knockBackResist = 0.5f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 150f;

            // 启用重力与碰撞（陆地敌）
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            NPC.aiStyle = -1;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (!Main.dayTime && spawnInfo.Player.ZoneBeach)
                return 0.6f;
            return 0f;
        }

        // 根据当前条件返回动画状态
        private JiaoShaState GetCurrentState()
        {
            if (dying)
                return JiaoShaState.Die;
            if (hurtTimer > 0)
                return JiaoShaState.Hurt;

            Player player = Main.player[NPC.target];
            if (player != null && player.active && !player.dead)
            {
                float dist = Vector2.Distance(NPC.Center, player.Center);
                // 当非常靠近且攻击冷却归零时，进入 Attack
                if (dist <= attackRange && attackCooldown <= 0)
                    return JiaoShaState.Attack;
            }
            if (!onGround)
                return JiaoShaState.Jump;
            if (player != null && player.active && !player.dead)
            {
                float dist = Vector2.Distance(NPC.Center, player.Center);
                if (dist <= chaseRange)
                    return JiaoShaState.Run;
            }
            return JiaoShaState.Idle;
        }

        public override void AI()
        {
            //目标检测与朝向设置
            NPC.TargetClosest(true);
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers)
            {
                DespawnLogic();
                return;
            }
            Player player = Main.player[NPC.target];

            // 默认使 NPC 面向玩家
            NPC.spriteDirection = player.Center.X < NPC.Center.X ? -1 : 1;

            // 玩家死亡时淡出
            if (player.dead)
            {
                state = JiaoShaState.Idle;
                NPC.velocity.X = 0f;
                frameTimer++;
                if (frameTimer > 10)
                {
                    frameTimer = 0;
                    currentFrame++;
                }
                deathIdleTimer++;
                if (deathIdleTimer > 300)
                {
                    NPC.alpha += 5;
                    if (NPC.alpha >= 255)
                        NPC.active = false;
                }
                return;
            }
            deathIdleTimer = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.timeLeft = 300;

            // 判断是否在地面
            onGround = (NPC.collideY && NPC.velocity.Y >= 0f);
            if (onGround && jumpCooldown > 0)
                jumpCooldown--;

            // 死亡处理
            if (dying)
            {
                DoDieLogic();
                return;
            }

            // 状态更新：若受伤，则 Hurt；否则根据与玩家距离更新状态
            if (hurtTimer > 0)
            {
                state = JiaoShaState.Hurt;
            }
            else
            {
                float dist = Vector2.Distance(NPC.Center, player.Center);
                if (dist <= attackRange && attackCooldown <= 0)
                    state = JiaoShaState.Attack;
                else if (dist <= chaseRange)
                    state = JiaoShaState.Run;
                else
                    state = JiaoShaState.Idle;
            }

            // 根据状态执行逻辑
            switch (state)
            {
                case JiaoShaState.Idle:
                    NPC.velocity.X = 0f;
                    break;
                case JiaoShaState.Run:
                    DoRunLogic(player);
                    break;
                case JiaoShaState.Jump:
                    // 空中保持当前速度
                    break;
                case JiaoShaState.Attack:
                    DoAttackLogic(player, isSpecial: false);
                    break;
                case JiaoShaState.Hurt:
                    NPC.velocity.X = 0f;
                    break;
                case JiaoShaState.Die:
                    // 死亡状态由 DoDieLogic 处理
                    break;
            }

            // 应用重力
            if (!onGround)
                NPC.velocity.Y += gravity;

            // 额外击退效果叠加与衰减
            NPC.velocity += extraKnockbackForce;
            extraKnockbackForce *= 0.9f;

            // 攻击与受伤冷却递减
            if (attackCooldown > 0)
                attackCooldown--;
            if (hurtTimer > 0)
                hurtTimer--;

            // 更新动画计时器
            animationCounter += 1f;

            // 平台检测及穿越设置
            bool platformHere = false;
            for (int i = (int)(NPC.Bottom.X / 16); i <= (int)((NPC.Bottom.X + NPC.width) / 16); i++)
            {
                for (int j = (int)((NPC.Bottom.Y - 2) / 16); j <= (int)((NPC.Bottom.Y + 2) / 16); j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (tile != null && tile.HasTile && Main.tileSolidTop[tile.TileType])
                    {
                        platformHere = true;
                        break;
                    }
                }
                if (platformHere)
                    break;
            }
            if (platformHere)
            {
                // 计算 NPC 与玩家的垂直距离
                float vertDist = Math.Abs(player.Center.Y - NPC.Bottom.Y);
                // 如果玩家在蛟鲨上方（玩家高出蛟鲨超过 16 像素），则允许跳跃（不掉下平台）
                if (player.Center.Y < NPC.Bottom.Y - 16f)
                {
                    NPC.noTileCollide = false;
                    // 在跑动逻辑中会处理跳跃
                }
                else
                {
                    // 当玩家与 NPC 垂直距离较大时，且 NPC 正在下落，则允许穿越平台
                    if (NPC.velocity.Y > 1f && vertDist > 32f)
                        NPC.noTileCollide = true;
                    else
                        NPC.noTileCollide = false;
                }
            }
            else
            {
                NPC.noTileCollide = false;
            }

            // invincibleTimer 处理：当目标不可见时沿反方向离开
            if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, player.position, player.width, player.height))
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
                    NPC.spriteDirection = initialVelocity.X > 0 ? 1 : -1;
                }
            }
            else
            {
                invincibleTimer = 0;
            }
        }

        private void DoRunLogic(Player player)
        {
            // 先根据水平位置设定移动
            if (player.Center.X > NPC.Center.X)
            {
                NPC.direction = 1;
                NPC.spriteDirection = 1;
                NPC.velocity.X = runSpeed;
            }
            else
            {
                NPC.direction = -1;
                NPC.spriteDirection = -1;
                NPC.velocity.X = -runSpeed;
            }

            // 跳跃逻辑：
            // 如果玩家明显高于蛟鲨，强制跳跃以向上追赶
            if (onGround && jumpCooldown <= 0)
            {
                if (player.Center.Y < NPC.Center.Y - 32f)
                {
                    NPC.velocity.Y = -9f - Main.rand.Next(0, 3);
                    jumpCooldown = 20;
                }
                else
                {
                    // 检查前方障碍，若存在障碍且玩家水平位置低于或接近蛟鲨，则尝试跳跃
                    int checkX = (int)((NPC.position.X + (NPC.direction == 1 ? NPC.width : 0)) / 16f) + (NPC.direction == 1 ? 2 : -2);
                    int checkY = (int)((NPC.position.Y + NPC.height - 4) / 16f);
                    if (Main.tile[checkX, checkY] != null && Main.tile[checkX, checkY].HasTile)
                    {
                        // 仅当玩家水平位置低于或接近时才跳跃，避免攻击时跳跃导致平台脱离
                        if (player.Center.Y >= NPC.Center.Y - 4)
                        {
                            NPC.velocity.Y = -6f - Main.rand.Next(0, 3);
                            jumpCooldown = 20;
                        }
                    }
                }
            }
        }

        private void DoAttackLogic(Player player, bool isSpecial)
        {
            NPC.velocity.X = 0f;
            attackAnimTimer++;
            int totalAttackDuration = 40; // 攻击动画总时长设为 40 帧
            if (!didDamageThisAttack && attackAnimTimer == 20)
            {
                float dist = Vector2.Distance(NPC.Center, player.Center);
                float range = isSpecial ? attackRange * 1.2f : attackRange;
                if (dist <= range)
                {
                    player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                    Vector2 knockbackDir = (player.Center - NPC.Center);
                    if (knockbackDir != Vector2.Zero)
                        knockbackDir.Normalize();
                    else
                        knockbackDir = new Vector2(NPC.spriteDirection, 0);
                    float knockPower = isSpecial ? 8f : 10f;
                    player.velocity += knockbackDir * knockPower;
                }
                didDamageThisAttack = true;
            }
            if (attackAnimTimer >= totalAttackDuration)
            {
                didDamageThisAttack = false;
                attackAnimTimer = 0;
                attackCooldown = 60;
                state = JiaoShaState.Idle;
            }
        }

        private void DoDieLogic()
        {
            if (dieTimer == 0)
                currentFrame = 0;
            NPC.velocity.X = 0f;
            dieTimer++;
            if (dieTimer > (DieFrameCount * frameDuration) + 10)
            {
                NPC.NPCLoot();
                NPC.active = false;
            }
            frameTimer++;
            if (frameTimer >= 10)
            {
                frameTimer = 0;
                currentFrame++;
                if (currentFrame >= DieFrameCount)
                    currentFrame = DieFrameCount - 1;
            }
        }

        private void DespawnLogic()
        {
            NPC.velocity.X = 0f;
            NPC.velocity.Y -= 0.2f;
            if (NPC.timeLeft > 10)
                NPC.timeLeft = 10;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (!dying && NPC.life <= 0)
            {
                dying = true;
                NPC.life = 1; // 防止重复死亡
                NPC.dontTakeDamage = true;
                NPC.netUpdate = true;
                state = JiaoShaState.Die;
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
                                               -hit.Knockback * factor * 0.05f);
            extraKnockbackForce += extraForce;
            hurtTimer = 20;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            JiaoShaState drawState = state;
            Texture2D texture;
            int totalFrames = 1;
            switch (drawState)
            {
                case JiaoShaState.Attack:
                    texture = attackTexture;
                    totalFrames = AttackFrameCount;
                    break;
                case JiaoShaState.Die:
                    texture = dieTexture;
                    totalFrames = DieFrameCount;
                    break;
                case JiaoShaState.Hurt:
                    texture = hurtTexture;
                    totalFrames = HurtFrameCount;
                    break;
                case JiaoShaState.Jump:
                    texture = jumpTexture;
                    totalFrames = JumpFrameCount;
                    break;
                case JiaoShaState.Run:
                    texture = runTexture;
                    totalFrames = RunFrameCount;
                    break;
                default:
                    texture = idleTexture;
                    totalFrames = IdleFrameCount;
                    break;
            }

            int frameHeight = texture.Height / totalFrames;
            int drawFrame;
            if (dying)
                drawFrame = Math.Min((int)(dieTimer / (float)frameDuration), totalFrames - 1);
            else if (drawState == JiaoShaState.Attack)
                drawFrame = (attackAnimTimer / frameDuration) % totalFrames;
            else
                drawFrame = (int)(animationCounter / frameDuration) % totalFrames;
            Rectangle sourceRect = new Rectangle(0, drawFrame * frameHeight, texture.Width, frameHeight);
            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight);
            SpriteEffects effects = (NPC.spriteDirection == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(texture, drawPos, sourceRect, drawColor * NPC.Opacity, NPC.rotation, origin, 1.0f, effects, 0f);
            return false;
        }
    }
}

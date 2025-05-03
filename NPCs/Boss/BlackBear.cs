using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using AncientChineseMythology.Items;
using System;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using static AncientChineseMythology.AncientChineseMythology;
using AncientChineseMythology.Systems;

namespace AncientChineseMythology.NPCs.Boss
{
    [AutoloadBossHead]
    public class BlackBear : ModNPC
    {
        private enum BearState
        {
            Idle,
            Run,
            Attack_1,
            Attack_2,
            Attack_3,
            Die
        }

        private BearState currentState = BearState.Idle;

        // 动画帧控制
        private int currentFrame = 0;
        private int frameTimer = 0;

        // 攻击控制
        private bool didDamageThisAttack = false;

        // 攻击后冷却
        private int cooldownTimer = 180;
        private int coolCount = 0;
        private bool isShootDiadema = false;
        // 攻击距离
        private float attackRange = 180f;

        // 运动
        private float runSpeed = 13.0f;
        private float distanceThreshold = 200f;
        private bool onGround = false;
        private int jumpCooldown = 0;

        // 精灵图
        private Texture2D dieTexture;
        private Texture2D attackTexture_1;
        private Texture2D attackTexture_1_1;
        private Texture2D attackTexture_2;
        private Texture2D attackTexture_2_2;
        private Texture2D runTexture;
        private Texture2D runTexture_1;
        private Texture2D idleTexture;
        private Texture2D idleTexture_1;

        private int deathIdleTimer = 0;

        // 死亡
        private bool isDying = false;
        private int dieTimer = 0;

        // 用于“碰到玩家就伤害”的冷却
        private int contactDamageCooldown = 0;
        private int contactDamageMaxCD = 30; // 0.5秒

        // 追踪玩家的时间
        private int runTime = 0;

        // 使用静态占位图
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear";
        // Boss头像
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults()
        {
            dieTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/die_328").Value;
            attackTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_1").Value;
            attackTexture_2 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_2").Value;
            attackTexture_1_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_1_1").Value;
            attackTexture_2_2 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_2_2").Value;

            runTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/run_332").Value;
            runTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/run_332_1").Value;
            idleTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/idle_344").Value;
            idleTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/idle_344_1").Value;

            NPC.width = 220; // 假设待命动画有4帧
            NPC.height = 280; // 待命动画每帧高度为344
            NPC.damage = 55;
            NPC.defense = 20;
            NPC.lifeMax = 8888;
            NPC.knockBackResist = 0f;
            NPC.value = Item.buyPrice(0, 10, 0, 0);
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.boss = true;
            NPC.HitSound = SoundID.NPCHit1;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/BlackBear_Music");
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (spawnInfo.Player.ZoneJungle && spawnInfo.Player.ZoneOverworldHeight && Main.dayTime)
            {
                return 0.005f;
            }
            return 0f;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers)
            {
                DespawnLogic();
                return;
            }
            Player player = Main.player[NPC.target];
            if (player.dead)
            {
                NPC.noTileCollide = false; // NPC 与地块发生碰撞
                // 保持 Idle 状态，Boss原地不动
                currentState = BearState.Idle;
                NPC.velocity.X = 0f;
                //Boos动画
                frameTimer++;
                if (frameTimer > 10)
                {
                    frameTimer = 0;
                    currentFrame++;
                }
                // 增加死亡后等待时间
                deathIdleTimer++;
                // 5秒后开始淡出（5秒=300 tick）
                if (deathIdleTimer > 300)
                {
                    // 每 tick 增加透明度 5（你可以根据需要调整淡出速度）
                    NPC.alpha += 5;
                    if (NPC.alpha >= 255)
                    {
                        // 完全透明后关闭 Boss
                        NPC.active = false;
                    }
                }
                return;
            }

            // 重置死亡等待计时器（如果玩家活着）
            deathIdleTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.timeLeft = 300;

            // 检测是否在地面（使用 collideY 判断）
            onGround = NPC.collideY && NPC.velocity.Y >= 0f;

            if (isDying)// 死亡阶段
            {
                NPC.damage = 0;
                if(dieTimer == 0)
                    currentFrame = 0;
                NPC.velocity.X = 0f;
                dieTimer++;
                int totalDie = 6; // 死亡动画有6帧
                if (dieTimer > (totalDie * 13)*2)
                {
                    DownedBossSystem.downedBlackBear = true;// 在boos列表中标记 Boss 已死亡
                    SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), player.Center);
                    NPC.NPCLoot();
                    NPC.active = false;
                }
            }
            else
            {
                switch (currentState)// 根据状态切换动画
                {
                    // 移动
                    case BearState.Run:
                        float dist = Vector2.Distance(NPC.Center, player.Center);
                        if ((player.Center.Y + NPC.height < NPC.Center.Y && (dist > distanceThreshold * 1.5 && runTime >= 180)) && onGround || (runTime >= 240))
                        {
                            currentState = BearState.Attack_2;
                            runTime = 0;
                            didDamageThisAttack = true;
                            NPC.velocity.X = NPC.direction * runSpeed;
                        }
                        else
                        {
                            runTime++;
                        }
                        
                        if (dist < distanceThreshold && onGround)
                        {
                            currentState = BearState.Attack_1;
                            didDamageThisAttack = true;
                        }

                        // 控制 NPC 的左右移动
                        if (player.position.X > NPC.position.X)
                        {
                            NPC.spriteDirection = 1;
                            NPC.direction = 1; // 向右移动
                        }
                        else
                        {
                            NPC.spriteDirection = -1;
                            NPC.direction = -1; // 向左移动
                        }

                        // 控制 NPC 的跳跃
                        // 检测前方是否有障碍物
                        int checkDistance = 2; // 用于决定检查的距离
                        int tileX = (int)((NPC.position.X + (NPC.direction == 1 ? NPC.width : 0)) / 16) + (NPC.direction == 1 ? checkDistance : -checkDistance); // 获取前方的方块位置
                        int tileY = (int)((NPC.position.Y + NPC.height) / 16); // NPC底部的Y坐标，稍微向上偏移一点

                        bool isOnPlatform = false; // 是否在平台上

                        // 检查 tile 是否存在
                        if (Main.tile[tileX, tileY] != null && Main.tile[tileX, tileY].HasTile)
                        {
                            if (Main.tile[tileX, tileY].TileType == TileID.Platforms)
                            {
                                isOnPlatform = true; // NPC在平台上
                            }
                        }

                        // 在平台上，NPC会跟随玩家上下移动
                        if (Math.Abs(player.Center.Y - NPC.Center.Y) < 800) // 如果玩家上下的距离在一定范围内
                        {
                            if (player.Center.Y > NPC.Center.Y + NPC.height/2 && isOnPlatform) // 玩家在NPC下方
                            {
                                NPC.velocity.Y += 0.1f; // 向下移动
                                NPC.noTileCollide = true; // NPC 不与地块发生碰撞
                            }
                            else
                            {
                                NPC.noTileCollide = false; // NPC 与地块发生碰撞
                            }
                        }

                        if (Math.Abs(player.position.X - NPC.position.X) < 200 && player.position.Y > NPC.position.Y + NPC.height)
                        {
                            if (isOnPlatform)
                            {
                                NPC.velocity.Y += 0.1f; // 向下移动
                                NPC.noTileCollide = true; // NPC 不与地块发生碰撞
                            }
                        }

                        // 控制 NPC 的水属性
                        if (NPC.wet)
                        {
                            if (NPC.honeyWet)
                            { // 消除蜂蜜的下落速率影响，使 NPC 在蜂蜜中正常下落
                                NPC.GravityMultiplier /= NPC.GravityWetMultipliers[LiquidID.Honey];
                                NPC.MaxFallSpeedMultiplier /= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey];
                            }
                            else if (!NPC.lavaWet && !NPC.shimmerWet)
                            { // 移除水的下落速度影响，然后添加蜂蜜的下落速度影响，使 NPC 在水中以蜂蜜的速度下落
                                NPC.GravityMultiplier *= NPC.GravityWetMultipliers[LiquidID.Honey] / NPC.GravityWetMultipliers[LiquidID.Water];
                                NPC.MaxFallSpeedMultiplier *= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey] / NPC.MaxFallSpeedWetMultipliers[LiquidID.Water];
                            }
                        }

                        if (NPC.collideY || NPC.collideX)
                        {
                            {
                                if (NPC.life > NPC.lifeMax * 0.5f)
                                {
                                    NPC.direction = (player.Center.X > NPC.Center.X) ? 1 : -1;
                                    NPC.spriteDirection = NPC.direction;
                                    if (NPC.Center.X - player.Center.X > 600 || player.Center.X - NPC.Center.X > 600)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 6);
                                    else if (NPC.Center.X - player.Center.X > 400 || player.Center.X - NPC.Center.X > 400)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 7);
                                    else if (NPC.Center.X - player.Center.X > 200 || player.Center.X - NPC.Center.X >200)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 8);
                                }
                                else
                                {
                                    NPC.direction = (player.Center.X > NPC.Center.X) ? 1 : -1;
                                    NPC.spriteDirection = NPC.direction;
                                    if (NPC.Center.X - player.Center.X > 600 || player.Center.X - NPC.Center.X > 600)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 4);
                                    else if (NPC.Center.X - player.Center.X > 400 || player.Center.X - NPC.Center.X > 400)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 5);
                                    else if (NPC.Center.X - player.Center.X > 200 || player.Center.X - NPC.Center.X > 200)
                                        NPC.velocity.X = NPC.direction * (runSpeed - 6);
                                }
                            }
                        }
                        if(currentState == BearState.Run && NPC.position == NPC.oldPosition
                            )
                            NPC.velocity.Y = -12f - Main.rand.Next(0, 5); // 设置跳跃的速度
                        if (NPC.velocity.Y < -10f)
                            NPC.noTileCollide = true;// 不与物块碰撞 
                        
                        // 如果前方有方块，则跳跃
                        if (Main.tile[tileX, tileY] != null && Main.tile[tileX, tileY].HasTile)// 这里需要修改，NPC.position.X 应该为 NPC.position.X + NPC.width / 2
                        {
                            if (NPC.collideX)
                            {
                                NPC.velocity.Y = -12f - Main.rand.Next(0, 5); // 设置跳跃的速度
                            }
                            ////如果玩家在NPC上方，则向上跳跃
                            if (NPC.Center.Y > player.Center.Y && NPC.Center.X < player.Center.X + NPC.width
                                && NPC.Center.X > player.Center.X - NPC.width&& NPC.collideY)
                            {
                                NPC.velocity.Y = -12f - (NPC.Center.Y - player.Center.Y) / 30f; // 设置跳跃的速度
                            }
                        }
                        break;
                    // 攻击
                    case BearState.Attack_1:
                        // 攻击时原地减速
                        NPC.velocity.X *= 0f;
                        if (didDamageThisAttack && currentFrame == 6)
                        {
                            PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 6f, 6f, 60, 1000f, FullName);// 定义屏幕震动
                            Main.instance.CameraModifiers.Add(modifier);// 屏幕震动
                            SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1"), NPC.Center);
                            SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), player.Center);
                            didDamageThisAttack = false;
                            float attackDist = Vector2.Distance(NPC.Center, player.Center);
                            if (attackDist <= attackRange*4)
                            {
                                // 计算击退方向：从 Boss 指向玩家
                                Vector2 knockbackDir = (player.Center - NPC.Center);
                                if (knockbackDir != Vector2.Zero)
                                    knockbackDir.Normalize();
                                else
                                    knockbackDir = new Vector2(NPC.spriteDirection, 0);
                                int projectileType = ModContent.ProjectileType<BlackBear_Proj1>();
                                // 发射主弹幕
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(-NPC.width / 6, -NPC.height / 12), NPC.velocity * 0, projectileType, 0, Main.myPlayer);
                                if (attackDist <= attackRange*2)
                                {
                                    // 造成伤害，并施加强大击退
                                    player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                                    player.velocity += knockbackDir * 10f;
                                    //粒子效果
                                    for (int i = 0; i < 6; i++)// 粒子效果
                                    {
                                        int dust = Dust.NewDust(player.position + player.velocity, player.width, player.height,
                                        DustID.YellowStarfish, player.velocity.X * 1f, NPC.velocity.Y * 1f);
                                        Main.dust[dust].color = Color.LightYellow; // 设置颜色
                                        Main.dust[dust].scale = 1.5f; // 设置大小
                                    }
                                    for (int i = 0; i < 4; i++)// 粒子效果
                                    {
                                        int dust = Dust.NewDust(player.position + player.velocity, player.width, player.height,
                                        DustID.YellowStarfish, player.velocity.X * 1f, NPC.velocity.Y * 1f);
                                        Main.dust[dust].color = Color.DarkGoldenrod; // 设置颜色
                                        Main.dust[dust].scale = 1.5f; // 设置大小
                                    }
                                }
                            }
                        }
                        if (coolCount == 0)
                            currentFrame = 0;

                        coolCount++;
                        // 当攻击动画播放完毕，进入冷却（5秒）
                        if (coolCount >= 50) // 攻击动画有10帧
                        {
                            didDamageThisAttack = false;
                            coolCount = 0;
                            cooldownTimer = 180;
                            currentState = BearState.Idle;
                        }
                        break;
                    case BearState.Attack_2:
                        // 停止移动
                        NPC.velocity.X = 0f;

                        // 发射 8 到 12 个 BlackBear_Proj2 弹幕
                        if (currentFrame == 6 && didDamageThisAttack)
                        {
                            SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), player.Center);
                            didDamageThisAttack = false;
                            int projectileCount = Main.rand.Next(8, 12);
                            for (int i = 0; i < projectileCount; i++)
                            {
                                int projectileType = ModContent.ProjectileType<BlackBear_Proj2>();
                                Vector2 spawnPosition = NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width / 2, NPC.width / 2), -NPC.height / 2);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPosition, Vector2.Zero, projectileType, NPC.damage/2, 0, Main.myPlayer);
                            }
                        }
                        if (coolCount == 0)
                            currentFrame = 0;
                        coolCount++;
                        // 当攻击动画播放完毕，进入冷却（5秒）
                        if (coolCount >= 50) // 攻击动画有10帧
                        {
                            didDamageThisAttack = false;
                            coolCount = 0;
                            cooldownTimer = 180;
                            currentState = BearState.Idle;
                        }
                        break;

                    // 待命
                    case BearState.Idle:
                        NPC.direction = (player.Center.X > NPC.Center.X) ? 1 : -1;
                        NPC.spriteDirection = NPC.direction;
                        if (cooldownTimer < 0)
                        {
                            currentState = BearState.Run;
                        }
                        else
                        {
                            cooldownTimer--;
                            //Main.NewText(cooldownTimer);
                            currentState = BearState.Idle;
                            NPC.velocity.X = 0f;
                            NPC.noTileCollide = false; // NPC 与地块发生碰撞
                        }
                        break;
                    // 死亡
                    case BearState.Die:
                        NPC.direction = (player.Center.X > NPC.Center.X) ? 1 : -1;
                        NPC.spriteDirection = NPC.direction;
                        break;
                }
                if (NPC.life < NPC.lifeMax * 0.5f)
                {
                    if (!isShootDiadema)
                    {
                        int projectileType = ModContent.ProjectileType<BlackBear_Proj3>();
                        // 发射主弹幕
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, -172), NPC.velocity * 0, projectileType, NPC.damage, Main.myPlayer);
                        isShootDiadema = true;
                    }
                }
                if (!onGround)
                    NPC.velocity.Y += 0.3f;
                if (jumpCooldown > 0)
                    jumpCooldown--;
            }

            // 每帧检查接触伤害
            CheckContactDamage();

            // 更新动画计时
            frameTimer++;
            if ((currentState == BearState.Idle && frameTimer >= 12) ||
                ((currentState == BearState.Attack_1 || currentState == BearState.Attack_2) && frameTimer >= 6) || 
                (currentState == BearState.Run && frameTimer >= 8) ||
                (currentState == BearState.Die && frameTimer >= 10 && currentFrame < 5))
            {
                frameTimer = 0;
                currentFrame++;
            }
        }
       
        private void CheckContactDamage()
        {
            // 如果正在死亡状态，则不检测接触伤害
            if (NPC.damage == 0)
                return;
                        
            if (contactDamageCooldown > 0)
                return;

            // 扩大碰撞区域，使 Boss 贴图边缘也能伤害玩家
            Rectangle expandedHitbox = NPC.Hitbox;
            expandedHitbox.Inflate(NPC.width / 2, NPC.height / 2);

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                if (expandedHitbox.Intersects(p.Hitbox))
                {
                    p.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.direction);
                    for (int j = 0; j < 5; j++)
                    {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, 0, 100, Color.OrangeRed, 1.5f);
                        d.noGravity = true;
                    }
                    contactDamageCooldown = contactDamageMaxCD;
                    break;
                }
            }
        }
        public override void HitEffect(NPC.HitInfo hit)
        {
            //死亡动画
            if (!isDying && NPC.life <= 0)
            {
                isDying = true;
                NPC.life = 1; // 确保 NPC 不会被重复击杀
                NPC.dontTakeDamage = true; // 防止在播放死亡动画时受到伤害
                NPC.netUpdate = true;
                currentState = BearState.Die;
                AncientChineseMythologySystem.downedBlackBear = true;
            }
        }
        
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (currentState == BearState.Run)
            {
                modifiers.FinalDamage *= 0.8f;// 减少伤害
            }
        }
        private void DespawnLogic()
        {
            NPC.velocity.X = 0f;
            NPC.velocity.Y -= 0.2f;
            if (NPC.timeLeft > 10)
                NPC.timeLeft = 10;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Main.dedServ)
                return true;

            Texture2D texture;
            int frameHeight;
            int totalFrames;

            switch (currentState)
            {
                case BearState.Die:
                    texture = dieTexture;
                    frameHeight = 328;
                    totalFrames = 6;
                    break;
                case BearState.Attack_1:
                    if(NPC.life > NPC.lifeMax * 0.5f)
                        texture = attackTexture_1;
                    else
                        texture = attackTexture_1_1;
                    frameHeight = 328;
                    totalFrames = 10;
                    break;
                case BearState.Attack_2:
                    if (NPC.life > NPC.lifeMax * 0.5f)
                        texture = attackTexture_2;
                    else
                        texture = attackTexture_2_2;
                        frameHeight = 328;
                    totalFrames = 10;
                    break;
                case BearState.Run:
                    if (NPC.life > NPC.lifeMax * 0.5f)
                        texture = runTexture;
                    else
                        texture = runTexture_1;
                    frameHeight = 332;
                    totalFrames = 6;
                    break;
                default:
                    if (NPC.life > NPC.lifeMax * 0.5f)
                        texture = idleTexture;
                    else
                        texture = idleTexture_1;
                    frameHeight = 344;
                    totalFrames = 4;
                    break;
            }

            int index = currentFrame % totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, index * frameHeight, texture.Width, frameHeight);

            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight);
            SpriteEffects effects = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(texture, drawPos, sourceRectangle, drawColor*NPC.Opacity, 0f, origin, 1f, effects, 0f);
            return false;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 5, 10));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SkyKey>(), 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearStaff>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearSword>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearBow>(), 10, 1));
        }
    }
}
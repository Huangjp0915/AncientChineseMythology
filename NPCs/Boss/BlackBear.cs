using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using AncientChineseMythology.Systems;
using Terraria.GameContent.ItemDropRules;
using AncientChineseMythology.Items;

namespace AncientChineseMythology.NPCs.Boss
{
    [AutoloadBossHead]
    public class BlackBear : ModNPC
    {
        private enum BearState
        {
            Idle,
            Run,
            Attack,
            Die
        }

        private BearState currentState = BearState.Idle;

        // 动画帧控制
        private int currentFrame = 0; 
        private int frameTimer = 0;  
        private int frameSpeed = 5;  

        private bool didDamageThisAttack = false;

        // 攻击后冷却
        private int cooldownTimer = 0;
        private int cooldownMax = 300; 

        private float attackRange = 180f; 

        // 运动
        private float runSpeed = 13.0f;
        private float distanceThreshold = 200f; 
        private bool onGround = false;
        private int jumpCooldown = 0;

        public static List<Texture2D> IdleFrames = new();
        public static List<Texture2D> RunFrames = new();
        public static List<Texture2D> AttackFrames = new();
        public static List<Texture2D> DieFrames = new();

        private int deathIdleTimer = 0;


        // 死亡
        private bool isDying = false;
        private int dieTimer = 0;

        // 用于“碰到玩家就伤害”的冷却
        private int contactDamageCooldown = 0;
        private int contactDamageMaxCD = 30; // 0.5秒

        // 使用静态占位图
        public override string Texture => "AncientChineseMythology/Textures/BlackBear/idle_01";
        // Boss头像
        public override string BossHeadTexture => "AncientChineseMythology/Textures/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults()
        {
            Texture2D staticTex = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/BlackBear/idle_01").Value;
            NPC.width = staticTex.Width;
            NPC.height = staticTex.Height;
            NPC.damage = 70;
            NPC.defense = 20;
            NPC.lifeMax = 8888;
            NPC.knockBackResist = 0.2f;
            NPC.value = Item.buyPrice(0, 10, 0, 0);
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.boss = true;
            Music = MusicID.Boss1;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (spawnInfo.Player.ZoneJungle && spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime)
            {
                return 0.005f;
            }
            return 0f;
        }

        public override void Load()
        {
            if (Main.dedServ) return;

            // Idle: 4帧 => idle_01 ... idle_04
            for (int i = 1; i <= 4; i++)
            {
                string path = $"AncientChineseMythology/Textures/BlackBear/idle_{i:00}";
                IdleFrames.Add(ModContent.Request<Texture2D>(path).Value);
            }

            // Run: 6帧 => run_01 ... run_06
            for (int i = 1; i <= 6; i++)
            {
                string path = $"AncientChineseMythology/Textures/BlackBear/run_{i:00}";
                RunFrames.Add(ModContent.Request<Texture2D>(path).Value);
            }

            // Attack: 10帧 => attack_01 ... attack_10
            for (int i = 1; i <= 10; i++)
            {
                string path = $"AncientChineseMythology/Textures/BlackBear/attack_{i:00}";
                AttackFrames.Add(ModContent.Request<Texture2D>(path).Value);
            }

            // Die: 6帧 => die_01 ... die_06
            for (int i = 1; i <= 6; i++)
            {
                string path = $"AncientChineseMythology/Textures/BlackBear/die_{i:00}";
                DieFrames.Add(ModContent.Request<Texture2D>(path).Value);
            }

            Main.NewText($"[BlackBearTextureSystem] Idle={IdleFrames.Count}, Run={RunFrames.Count}, Attack={AttackFrames.Count}, Die={DieFrames.Count}", Microsoft.Xna.Framework.Color.LightGreen);
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
                // 保持 Idle 状态，Boss原地不动
                currentState = BearState.Idle;
                NPC.velocity.X = 0f;

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

            // 如果HP<=0，则进入死亡阶段
            if (NPC.life <= 0 && !isDying)
            {
                isDying = true;
                dieTimer = 0;
                currentState = BearState.Die;
            }

            if (isDying)
            {
                NPC.velocity.X = 0f;
                dieTimer++;
                int totalDie = BlackBearTextureSystem.DieFrames?.Count ?? 1;
                if (dieTimer > totalDie * frameSpeed + 30)
                    NPC.active = false;
            }
            else
            {
                // 攻击后冷却期间，Boss站在原地不动
                if (cooldownTimer > 0)
                {
                    cooldownTimer--;
                    currentState = BearState.Idle;
                    NPC.velocity.X = 0f;
                }
                else
                {
                    float dist = Vector2.Distance(NPC.Center, player.Center);
                    if (dist > distanceThreshold)
                    {
                        currentState = BearState.Run;
                        didDamageThisAttack = false;
                    }
                    else
                    {
                        currentState = BearState.Attack;
                        didDamageThisAttack = false;
                    }
                }

                switch (currentState)
                {
                    case BearState.Run:
                        // 设置朝向：如果玩家在右边，Boss面向右边；否则向左
                        NPC.direction = (player.Center.X > NPC.Center.X) ? 1 : -1;
                        NPC.spriteDirection = NPC.direction;
                        NPC.velocity.X = NPC.direction * runSpeed;
                        // 若撞墙且在地面且跳冷却结束，则跳跃
                        if (NPC.collideX && onGround && jumpCooldown <= 0)
                        {
                            NPC.velocity.Y = -17f; 
                            jumpCooldown = 30;
                        }
                        break;

                    case BearState.Attack:
                        // 攻击时原地减速
                        NPC.velocity.X *= 0.8f;
                        if (!didDamageThisAttack)
                        {
                            float attackDist = Vector2.Distance(NPC.Center, player.Center);
                            if (attackDist < attackRange)
                            {
                                // 计算击退方向：从 Boss 指向玩家
                                Vector2 knockbackDir = (player.Center - NPC.Center);
                                if (knockbackDir != Vector2.Zero)
                                    knockbackDir.Normalize();
                                else
                                    knockbackDir = new Vector2(NPC.spriteDirection, 0);
                                // 造成伤害，并施加强大击退
                                player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                                player.velocity = knockbackDir * 200f + new Vector2(0, -200f);
                                // 产生特效
                                for (int i = 0; i < 10; i++)
                                {
                                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, 0, 100, Color.OrangeRed, 1.5f);
                                    d.noGravity = true;
                                }
                                didDamageThisAttack = true;
                            }
                        }
                        // 当攻击动画播放完毕，进入冷却（5秒）
                        if (BlackBearTextureSystem.AttackFrames != null && BlackBearTextureSystem.AttackFrames.Count > 0)
                        {
                            if (currentFrame >= BlackBearTextureSystem.AttackFrames.Count)
                            {
                                currentFrame = 0;
                                cooldownTimer = cooldownMax;
                            }
                        }
                        else
                        {
                            currentState = BearState.Idle;
                        }
                        break;

                    case BearState.Idle:
                        NPC.velocity.X *= 0.9f;
                        break;

                    case BearState.Die:
                        // 已由 isDying 处理
                        break;
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
            if (frameTimer >= frameSpeed)
            {
                frameTimer = 0;
                currentFrame++;
            }
        }

        private void CheckContactDamage()
        {
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
            if (BlackBearTextureSystem.IdleFrames == null || BlackBearTextureSystem.IdleFrames.Count == 0)
                return true;

            List<Texture2D> frames;
            int totalFrames;
            switch (currentState)
            {
                case BearState.Die:
                    frames = BlackBearTextureSystem.DieFrames;
                    totalFrames = frames?.Count ?? 1;
                    break;
                case BearState.Attack:
                    frames = BlackBearTextureSystem.AttackFrames;
                    totalFrames = frames?.Count ?? 1;
                    break;
                case BearState.Run:
                    frames = BlackBearTextureSystem.RunFrames;
                    totalFrames = frames?.Count ?? 1;
                    break;
                default:
                    frames = BlackBearTextureSystem.IdleFrames;
                    totalFrames = frames?.Count ?? 1;
                    break;
            }
            if (frames == null || frames.Count == 0)
                return true;

            int index;
            if (currentState == BearState.Die || currentState == BearState.Attack)
            {
                index = System.Math.Min(currentFrame, totalFrames - 1);
            }
            else
            {
                index = currentFrame % totalFrames;
            }
            if (index < 0 || index >= frames.Count)
                return true;
            Texture2D tex = frames[index];
            if (tex == null)
                return true;

            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height);
            SpriteEffects effects = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(tex, drawPos, null, drawColor, 0f, origin, 1f, effects, 0f);
            return false;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 5, 10));

            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SkyKey>(), 1));
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (NPC.life <= 0)
            {
                // 可添加 gore 效果
            }
        }
    }
}

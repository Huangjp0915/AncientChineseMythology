using System;
using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Waapons.SummoningStaffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Monsters
{
    public class MingCrow : ModNPC
    {
        // 动画状态枚举
        private enum AnimationState
        {
            Attack,
            Die,
            Fly,
            Hurt,
            Idle
        }

        // 每种动画 5 帧
        private const int FramesPerAnimation = 5;
        // 动画播放速度（每帧持续时间）
        private int frameDuration = 6;

        // 贴图
        private Texture2D attackTexture;
        private Texture2D dieTexture;
        private Texture2D flyTexture;
        private Texture2D hurtTexture;
        private Texture2D idleTexture;

        // 动画播放用计时器
        private float animationCounter = 0f;

        // 死亡相关
        private bool dying = false;
        private bool isDead = false;
        private int dieTimer = 0;

        // 攻击冷却
        private int attackCooldown = 0;

        // 新增：失敌或遮挡时的反方向离开处理
        private int invincibleTimer = 0;
        private Vector2 initialVelocity = Vector2.Zero;

        // 强制使用假的 Texture 路径
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/MingCrow";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            // 手动加载 5 帧竖排贴图
            attackTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/Attack").Value;
            dieTexture    = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/Die").Value;
            flyTexture    = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/Fly").Value;
            hurtTexture   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/Hurt").Value;
            idleTexture   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/MingCrow/Idle").Value;

            // NPC 基本属性
            NPC.width = 34;
            NPC.height = 28;
            NPC.damage = 13;
            NPC.defense = 5;
            NPC.lifeMax = 35;
            NPC.knockBackResist = 0.3f;
            NPC.HitSound = SoundID.NPCHit46;
            NPC.DeathSound = SoundID.NPCDeath48;
            NPC.value = 100f;

            // 飞行
            NPC.noGravity = true;
            NPC.noTileCollide = false;

            // 使用自定义 AI
            NPC.aiStyle = -1;
        }

        // 夜晚才 40% 概率生成
        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (!Main.dayTime && spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.townNPCs < 1)
                return 0.2f;
            return 0f;
        }

        // AI 状态判断
        private AnimationState GetCurrentState()
        {
            // 若死亡触发
            if (dying)
                return AnimationState.Die;

            // 攻击：若目标距离小于 60 且冷却结束
            Player target = Main.player[NPC.target];
            if (target != null && !target.dead)
            {
                float dist = Vector2.Distance(NPC.Center, target.Center);
                if (dist < 60f && attackCooldown <= 0)
                    return AnimationState.Attack;
            }

            // 受伤显示 Hurt
            if (NPC.life < NPC.lifeMax / 3)
                return AnimationState.Hurt;

            // 有速度则 Fly，否 Idle
            return (NPC.velocity.Length() > 1f) ? AnimationState.Fly : AnimationState.Idle;
        }

        public override void AI()
        {
            // 如果正在死亡，则执行死亡动画逻辑
            if (dying)
            {
                NPC.damage = 0;
                if (!isDead)
                {
                    animationCounter = 0f;
                    isDead = true;
                }
                animationCounter += 1f;
                NPC.velocity = Vector2.Zero;
                dieTimer++;
                // 死亡动画播放完后产生粒子效果并消失
                if (dieTimer > FramesPerAnimation * frameDuration + 10)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                                                        DustID.Torch, 0, 0, 100, Color.Purple, 1.2f);
                        dust.noGravity = true;
                    }
                    NPC.NPCLoot();
                    NPC.active = false;
                }
                return;
            }

            // 普通飞行 AI：追击玩家
            NPC.TargetClosest();
            Player targetPlayer = Main.player[NPC.target];
            if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
            {
                // 如果玩家在水中，则不追击，保持缓慢减速状态
                if (targetPlayer.wet)
                {
                    NPC.velocity *= 0.95f;
                }
                else
                {
                    Vector2 toPlayer = targetPlayer.Center - NPC.Center;
                    float dist = toPlayer.Length();
                    if (dist > 20f)
                        toPlayer.Normalize();

                    // 使用 Lerp 平滑追击
                    float speed = 8f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer * speed, 0.03f);

                    // 始终保持贴图与追击方向一致
                    NPC.spriteDirection = (targetPlayer.Center.X >= NPC.Center.X) ? -1 : 1;
                }
            }
            else
            {
                NPC.velocity *= 0.95f;
            }

            // 攻击冷却递减
            if (attackCooldown > 0)
                attackCooldown--;

            // 平台检测
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
            // 若在平台上且处于下落状态，则允许穿越平台
            if (onPlatform && NPC.velocity.Y > 0)
                NPC.noTileCollide = true;
            else
                NPC.noTileCollide = false;

            //当目标不可见时，启动 invincibleTimer，并在超过 120 帧后沿记录的反方向离开
            if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height, targetPlayer.position, targetPlayer.width, targetPlayer.height))
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
                    NPC.spriteDirection = initialVelocity.X > 0 ? -1 : 1;
                }
            }
            else
            {
                invincibleTimer = 0;
            }

            // 更新动画计时器
            animationCounter += 1f;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            // 如果血量 <=0，进入死亡流程
            if (!dying && NPC.life <= 0)
            {
                dying = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.netUpdate = true;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            AnimationState state = GetCurrentState();
            Texture2D texture = null;
            switch (state)
            {
                case AnimationState.Attack:
                    texture = attackTexture;
                    break;
                case AnimationState.Die:
                    texture = dieTexture;
                    break;
                case AnimationState.Fly:
                    texture = flyTexture;
                    break;
                case AnimationState.Hurt:
                    texture = hurtTexture;
                    break;
                default:
                    texture = idleTexture;
                    break;
            }

            int frame;
            if (dying)
            {
                // 直接计算死亡动画当前帧，不做循环：确保取值范围 [0, FramesPerAnimation-1]
                frame = Math.Min((int)(dieTimer / (float)frameDuration), FramesPerAnimation - 1);
            }
            else if (state == AnimationState.Attack)
            {
                frame = (attackCooldown == 0) ? (int)(animationCounter / frameDuration) % FramesPerAnimation : (int)(animationCounter / frameDuration) % FramesPerAnimation;
            }
            else
            {
                frame = (int)(animationCounter / frameDuration) % FramesPerAnimation;
            }
            int frameHeight = texture.Height / FramesPerAnimation;
            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);

            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);
            SpriteEffects effects = (NPC.spriteDirection == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(texture, drawPos, sourceRect, drawColor * NPC.Opacity, NPC.rotation, origin, 1f, effects, 0f);
            return false;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MingCrowStaff>(), 100));
        }
    }
}

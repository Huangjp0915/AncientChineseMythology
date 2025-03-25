using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Systems;
using Terraria.DataStructures; 

namespace AncientChineseMythology.NPCs
{
    public class angry_gargoyle : ModNPC
    {
        // 存储各状态动画帧，每帧为单独 PNG
        private List<Texture2D> runFrames = new List<Texture2D>();
        private List<Texture2D> attackFrames = new List<Texture2D>();
        private List<Texture2D> dieFrames = new List<Texture2D>();

        // 帧数（固定）
        private int runFrameCount = 4;
        private int attackFrameCount = 4;
        private int dieFrameCount = 6;

        // 每帧持续时间，单位为游戏帧
        private int frameDuration = 6;

        // 攻击冷却，单位帧
        private int attackCooldown = 0;

        // 在 Angry_gargoyle 类的字段声明处添加：
        private Vector2 extraKnockbackForce = Vector2.Zero;

        // 死亡动画控制
        private bool dying = false;
        private int dieTimer = 0;

        // 独立动画计时器
        private float animationCounter = 0f;

        // 覆盖 Texture 属性，返回一个假路径，防止 tModLoader 自动加载单一贴图
        public override string Texture => "AncientChineseMythology/Textures/angry_gargoyle/run_01";

        public override void Load()
        {
            // 加载 run 帧
            for (int i = 1; i <= runFrameCount; i++)
            {
                string path = $"AncientChineseMythology/Textures/angry_gargoyle/run_{i:D2}";
                Texture2D tex = ModContent.Request<Texture2D>(path).Value;
                runFrames.Add(tex);
            }
            // 加载 attack 帧
            for (int i = 1; i <= attackFrameCount; i++)
            {
                string path = $"AncientChineseMythology/Textures/angry_gargoyle/attack_{i:D2}";
                Texture2D tex = ModContent.Request<Texture2D>(path).Value;
                attackFrames.Add(tex);
            }
            // 加载 die 帧
            for (int i = 1; i <= dieFrameCount; i++)
            {
                string path = $"AncientChineseMythology/Textures/angry_gargoyle/die_{i:D2}";
                Texture2D tex = ModContent.Request<Texture2D>(path).Value;
                dieFrames.Add(tex);
            }
            Main.NewText($"Loaded textures: run={runFrames.Count}, attack={attackFrames.Count}, die={dieFrames.Count}", Color.Green);
        }

        public override void Unload()
        {
            runFrames.Clear();
            attackFrames.Clear();
            dieFrames.Clear();
        }

        public override void SetStaticDefaults()
        {
            // 设置为1帧，防止 tModLoader 默认竖直切割
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults()
        {
            NPC.width = 50;
            NPC.height = 50;
            NPC.damage = 15;
            NPC.defense = 8;
            NPC.lifeMax = 80;
            NPC.life = 80;
            NPC.knockBackResist = 0.7f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;
        }

        // 重写 SpawnChance，设置自然生成条件
        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            // 即使在白天，只要在开放世界且不在城镇区域，都有很高的生成几率
            if (spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.townNPCs < 1)
                return 0.95f; // 测试用高概率
            return 0f;
        }

        // 定义动画状态枚举
        private enum AnimationState
        {
            Run,
            Attack,
            Die
        }

        private AnimationState GetAnimationState()
        {
            // 如果 NPC 的生命<=0，则返回 Die 状态
            if (NPC.life <= 0)
                return AnimationState.Die;
            // 简单判断：如果目标玩家距离NPC较近，则攻击
            Player target = Main.player[NPC.target];
            if (target != null && Vector2.Distance(NPC.Center, target.Center) < 50f)
                return AnimationState.Attack;
            return AnimationState.Run;
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            float extraKnockbackFactor = 2.0f; // 可根据需要调整
            // 使用 hit.Knockback 和 hit.HitDirection 从 HitInfo 中获取数据
            Vector2 extraForce = new Vector2(hit.Knockback * extraKnockbackFactor * hit.HitDirection, -hit.Knockback * extraKnockbackFactor * 0.5f);
            extraKnockbackForce += extraForce;
        }


        public override void AI()
        {
            if (dying)
            {
                NPC.velocity = Vector2.Zero;
                dieTimer++;
                if (dieTimer > dieFrameCount * frameDuration + 30)
                    NPC.active = false;
                return;
            }

            // 自定义追击逻辑：
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (target != null)
            {
                Vector2 direction = target.Center - NPC.Center;
                float distance = direction.Length();
                if (direction != Vector2.Zero)
                    direction.Normalize();
                float speed = 2.5f;
                NPC.velocity = direction * speed;
                NPC.spriteDirection = (target.Center.X > NPC.Center.X) ? 1 : -1;

                if (distance < 70f && GetAnimationState() == AnimationState.Attack)
                {
                    if (attackCooldown <= 0)
                    {
                        // 触发攻击
                        target.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                        float knockbackForce = 4f;
                        target.velocity += direction * knockbackForce + new Vector2(0, -knockbackForce * 0.5f);

                        // 添加攻击特效，生成若干火花 dust
                        for (int i = 0; i < 10; i++)
                        {
                            Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, 0, 100, Color.Red, 1.5f);
                            dust.noGravity = true;
                        }
                        attackCooldown = 90;
                    }
                }
            }
            else
            {
                NPC.velocity = Vector2.Zero;
            }

            if (attackCooldown > 0)
                attackCooldown--;

            // 将额外击退力应用到 NPC 的速度上，并衰减
            NPC.velocity += extraKnockbackForce;
            extraKnockbackForce *= 0.9f; // 每帧衰减 10%

            animationCounter += 1f;
        }

        // 不使用默认 FindFrame，直接依赖 animationCounter

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            AnimationState state = GetAnimationState();
            List<Texture2D> frames;
            int totalFrames;
            switch (state)
            {
                case AnimationState.Attack:
                    frames = GargoyleTextureSystem.AttackFrames;
                    totalFrames = attackFrameCount;
                    break;
                case AnimationState.Die:
                    frames = GargoyleTextureSystem.DieFrames;
                    totalFrames = dieFrameCount;
                    break;
                default:
                    frames = GargoyleTextureSystem.RunFrames;
                    totalFrames = runFrameCount;
                    break;
            }

            if (frames == null || frames.Count == 0)
                return true;

            int currentFrame = (int)(animationCounter / frameDuration) % totalFrames;
            Texture2D currentTexture = frames[currentFrame];
            if (currentTexture == null)
                return true;

            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(currentTexture.Width / 2f, currentTexture.Height / 2f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float scale = 0.8f;
            spriteBatch.Draw(currentTexture, drawPos, null, Color.White, NPC.rotation, origin, scale, effects, 0f);

            return false;
        }
    }
}

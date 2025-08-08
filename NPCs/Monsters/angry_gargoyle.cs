using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Monsters
{
    public class angry_gargoyle : ModNPC
    {
        //帧数（固定）
        private int runFrameCount = 4;
        private int attackFrameCount = 4;
        private int dieFrameCount = 6;
        //失敌时间计数
        private int invincibleTimer = 0;
        private Vector2 initialVelocity = Vector2.Zero;

        //每帧持续时间，单位为游戏帧
        private int frameDuration = 6;

        //攻击冷却，单位帧
        private int attackCooldown = 0;

        //在 Angry_gargoyle 类的字段声明处添加：
        private Vector2 extraKnockbackForce = Vector2.Zero;

        //死亡动画控制
        private bool dying = false;
        private int dieTimer = 0;

        //死亡控制
        private bool isDead = false;

        //独立动画计时器
        private float animationCounter = 0f;

        //精灵图
        private Texture2D runTexture;
        private Texture2D attackTexture;
        private Texture2D dieTexture;

        //覆盖 Texture 属性，返回一个假路径，防止 tModLoader 自动加载单一贴图
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Monsters/angry_gargoyle/angry_gargoyle";

        //public override void Load()
        //{

        //}

        public override void SetStaticDefaults() {
            //设置为1帧，防止 tModLoader 默认竖直切割
            //Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            runTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/angry_gargoyle/run_48").Value;
            attackTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/angry_gargoyle/attack_48").Value;
            dieTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Monsters/angry_gargoyle/die_46").Value;

            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 12;
            NPC.defense = 8;
            NPC.lifeMax = 60;
            NPC.life = 60;
            NPC.knockBackResist = 0.7f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;
        }

        //重写 SpawnChance，设置自然生成条件
        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            //即使在白天，只要在开放世界且不在城镇区域，都有很高的生成几率
            if (spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.townNPCs < 1)
                return 0.32f; //测试用高概率
            return 0f;
        }

        //定义动画状态枚举
        private enum AnimationState
        {
            Run,
            Attack,
            Die
        }

        private AnimationState GetAnimationState()//获取当前动画状态
        {
            //如果 NPC 的生命<=0，则返回 Die 状态
            if (dying)
                return AnimationState.Die;
            //简单判断：如果目标玩家距离NPC较近，则攻击
            Player target = Main.player[NPC.target];
            if (target != null && Vector2.Distance(NPC.Center, target.Center) < 50f)
                return AnimationState.Attack;
            return AnimationState.Run;
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)//重写 OnHitByItem，添加额外击退力
        {
            float extraKnockbackFactor = 2.0f; //可根据需要调整
                                               //使用 hit.Knockback 和 hit.HitDirection 从 HitInfo 中获取数据
            Vector2 extraForce = new Vector2(hit.Knockback * extraKnockbackFactor * hit.HitDirection, -hit.Knockback * extraKnockbackFactor * 0.5f);
            extraKnockbackForce += extraForce;
        }

        public override void AI() {
            if (dying) {
                NPC.damage = 0;
                if (!isDead) {
                    animationCounter = 0f;
                    isDead = true;
                }

                NPC.velocity = Vector2.Zero;
                dieTimer++;
                if (animationCounter < 35) //35 帧的死亡动画切换时间，刚好到趴下
                    animationCounter += 1f;
                if (dieTimer > dieFrameCount * frameDuration + 10) //10 帧的缓冲时间
                {
                    for (int i = 0; i < 5; i++) //粒子效果
                    {
                        int dust = Dust.NewDust(NPC.position + NPC.velocity, NPC.width, NPC.height,
                        DustID.BlueCrystalShard, NPC.velocity.X * 1f, NPC.velocity.Y * 1f);
                        Main.dust[dust].color = Color.LightGreen; //设置颜色
                        Main.dust[dust].scale = 1.5f; //设置大小
                        //Main.dust[dust].noGravity = true; //禁止重力
                    }
                    NPC.NPCLoot();
                    NPC.active = false;
                }
                return;
            }
            //检查 NPC 是否在平台上
            bool onPlatform = false;

            //自定义追击逻辑：
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (target != null && invincibleTimer <= 120) {
                for (int i = (int)(NPC.Bottom.X / 16); i <= (int)((NPC.Bottom.X + NPC.width) / 16); i++) {
                    for (int j = (int)(NPC.Bottom.Y / 16); j <= (int)((NPC.Bottom.Y + 1) / 16); j++) {
                        Tile tile = Main.tile[i, j];
                        if (tile != null && tile.HasTile && Main.tileSolidTop[tile.TileType]) {
                            onPlatform = true;
                            break;
                        }
                    }
                    if (onPlatform)
                        break;
                }
                //设置 NPC 只在平台上时可以穿过
                NPC.noTileCollide = onPlatform;

                Vector2 direction = target.Center - NPC.Center;
                float distance = direction.Length();
                if (direction != Vector2.Zero)
                    direction.Normalize();
                float speed = 2.5f;
                NPC.velocity = direction * speed;
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

                if (distance < 70f && GetAnimationState() == AnimationState.Attack) {
                    if (attackCooldown <= 0) {
                        //触发攻击
                        target.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                        float knockbackForce = 4f;
                        target.velocity += direction * knockbackForce + new Vector2(0, -knockbackForce * 0.5f);

                        //添加攻击特效，生成若干蓝焰 dust
                        for (int i = 0; i < 3; i++) //改了下
                        {
                            Dust dust = Dust.NewDustDirect(NPC.position + direction * 40, NPC.width, NPC.height, DustID.FireworksRGB, NPC.velocity.X * 2f, NPC.velocity.Y * 2f, 100, Color.LightSkyBlue, 1f);

                            dust.noGravity = true;
                            dust.velocity = direction * 5f;
                        }
                        attackCooldown = 90;
                    }
                }
            }
            else {
                NPC.velocity = Vector2.Zero;
            }

            if (attackCooldown > 0)
                attackCooldown--;

            if (!onPlatform) {
                invincibleTimer++;
                if (invincibleTimer == 120) {
                    //在与玩家向量的反方向速度
                    initialVelocity = -NPC.velocity;
                    initialVelocity.Normalize();
                }
                if (invincibleTimer > 120) {
                    NPC.noTileCollide = onPlatform;
                    NPC.spriteDirection = initialVelocity.X > 0 ? 1 : -1;
                    //沿着上一帧速度方向移动
                    NPC.velocity = initialVelocity;
                }
            }
            if (Collision.CanHitLine(NPC.position, NPC.width, NPC.height, target.position, target.width, target.height)) {
                invincibleTimer = 0;
            }


            //将额外击退力应用到 NPC 的速度上，并衰减
            NPC.velocity += extraKnockbackForce;
            extraKnockbackForce *= 0.9f; //每帧衰减 10%

            animationCounter += 1f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //死亡动画
            if (!dying && NPC.life <= 0) {
                dying = true;
                NPC.life = 1; //确保 NPC 不会被重复击杀
                NPC.dontTakeDamage = true; //防止在播放死亡动画时受到伤害
                NPC.damage = 0;
                NPC.netUpdate = true;
            }
        }

        //不使用默认 FindFrame，直接依赖 animationCounter
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            AnimationState state = GetAnimationState();
            Texture2D texture = null;
            int frameHeight = 0;
            int totalFrames = 0;

            switch (state) {
                case AnimationState.Attack:
                    texture = attackTexture;
                    frameHeight = 48;
                    totalFrames = attackFrameCount;
                    break;
                case AnimationState.Die:
                    texture = dieTexture;
                    frameHeight = 46;
                    totalFrames = dieFrameCount;
                    break;
                default:
                    texture = runTexture;
                    frameHeight = 48;
                    totalFrames = runFrameCount;
                    break;
            }

            int currentFrame = (int)(animationCounter / frameDuration) % totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, currentFrame * frameHeight, texture.Width, frameHeight);

            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float scale = 0.8f;
            spriteBatch.Draw(texture, drawPos, sourceRectangle, Color.White, NPC.rotation, origin, scale, effects, 0f);

            return false;
        }
    }
}

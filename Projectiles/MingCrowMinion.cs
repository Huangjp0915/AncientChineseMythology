using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class MingCrowMinion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/MingCrowMinion/MingCrowMinion_Fly";

        //------- 动画资源 -------
        private Texture2D flyTexture;
        private Texture2D attackTexture;

        private const int FramesPerAnim = 5;
        private const float TeleportThreshold = 1200f;
        private const float IdleYOffset = 48f;

        private enum AnimState { Fly, Attack }
        private AnimState animState = AnimState.Fly;

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            Main.projFrames[Type] = FramesPerAnim;   //实际只用来存帧计数
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.minionSlots = 1f;
            Projectile.aiStyle = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            //动态加载两张贴图
            flyTexture = ModContent.Request<Texture2D>(
                "AncientChineseMythology/Textures/Projectiles/MingCrowMinion/MingCrowMinion_Fly").Value;
            attackTexture = ModContent.Request<Texture2D>(
                "AncientChineseMythology/Textures/Projectiles/MingCrowMinion/MingCrowMinion_Attack").Value;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            //--------- 存活检查 ----------
            if (player.dead || !player.active) {
                player.ClearBuff(ModContent.BuffType<Buffs.MingCrowMinionBuff>());
                Projectile.Kill();
                return;
            }

            if (player.HasBuff(ModContent.BuffType<Buffs.MingCrowMinionBuff>()))
                Projectile.timeLeft = 2;

            player.AddBuff(ModContent.BuffType<Buffs.MingCrowMinionBuff>(), 2);

            Vector2 idlePos = player.Center;
            idlePos.Y -= IdleYOffset;                 //位于玩家头顶 48 px 处
            Vector2 toIdle = idlePos - Projectile.Center;
            float distIdle = toIdle.Length();

            if (Main.myPlayer == player.whoAmI && distIdle > TeleportThreshold) {
                Projectile.position = idlePos;
                Projectile.velocity *= 0.1f;
                Projectile.netUpdate = true;
            }

            //--------- 寻敌 ----------
            int target = -1;
            float nearest = 600f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this)) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < nearest) {
                        target = i;
                        nearest = dist;
                    }
                }
            }

            //--------- 行为 ----------
            if (target != -1) {
                NPC npc = Main.npc[target];
                Vector2 toEnemy = npc.Center - Projectile.Center;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                                                   toEnemy.SafeNormalize(Vector2.Zero) * 12f,
                                                   0.15f);

                animState = nearest < 120f ? AnimState.Attack : AnimState.Fly;

                //近身造成接触伤害（带冷却）
                if (nearest < 40f) {
                    if (Projectile.ai[1] <= 0) {
                        Projectile.ai[1] = 20;   //1/3 秒 CD
                        NPC.HitInfo hit = new NPC.HitInfo {
                            Damage = Projectile.damage,
                            Knockback = 0f,
                            HitDirection = Projectile.direction,
                            Crit = false
                        };
                        npc.StrikeNPC(hit);
                    }
                }
            }
            else {
                //无目标：环绕玩家
                animState = AnimState.Fly;
                float radius = 60f;
                float angle = (Main.GameUpdateCount * 0.05f + Projectile.ai[0])
                               % MathHelper.TwoPi;
                Vector2 orbit = player.Center + radius * angle.ToRotationVector2();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                                                   (orbit - Projectile.Center)
                                                       .SafeNormalize(Vector2.Zero) * 8f,
                                                   0.12f);
            }

            if (Projectile.ai[1] > 0) Projectile.ai[1]--;   //近战 CD 递减

            //--- 若被固体方块困住，计时满 60 帧后瞬移到玩家身旁 ---
            if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.localAI[0]++;                 //卡墙帧计数
                if (Projectile.localAI[0] > 60) {
                    Projectile.position = player.Center; //瞬移
                    Projectile.velocity *= 0f;
                    Projectile.localAI[0] = 0;
                }
            }
            else {
                Projectile.localAI[0] = 0;               //清零计数
            }

            //朝向
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? -1 : 1;

            //--------- 帧动画 ----------
            if (++Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % FramesPerAnim;
            }
        }

        //---------- 自绘以切换贴图 ----------
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = animState == AnimState.Attack ? attackTexture : flyTexture;

            int frameH = tex.Height / FramesPerAnim;
            Rectangle src = new Rectangle(0, Projectile.frame * frameH,
                                          tex.Width, frameH);

            Vector2 origin = new(tex.Width * 0.5f, frameH * 0.5f);
            SpriteEffects fx = Projectile.spriteDirection == -1
                               ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Main.EntitySpriteDraw(tex,
                                  Projectile.Center - Main.screenPosition,
                                  src, lightColor,
                                  Projectile.rotation, origin, 1f, fx, 0);
            return false;      //阻止默认绘制
        }
    }
}

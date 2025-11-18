using AncientChineseMythology.NPCs;
using InnoVault;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙基础类
    /// </summary>
    public abstract class NetherDragon : BasicWorm
    {
        public override bool IsUseSpriteDirection => true;

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

        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.lifeMax = 80000;
            NPC.damage = 80;
            NPC.defense = 35;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            SummonMax = 60;
        }

        public override void AI()
        {
            base.AI();
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
            {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            if (FatherNPC.Alives()) {
                Vector2 pos = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;
                for (int i = 0; i < NPC.velocity.Length() / 2; i++) {
                    int dust = Dust.NewDust(pos, 1, 1, DustID.BlueTorch);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.6f);
                }
            }
            

            // 发光效果
            Lighting.AddLight(NPC.Center, 0.1f, 0.3f, 0.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);
            
            if (NPCWormType == WormType.Head)
            {
                origin.Y = tex.Height / 2;
            }

            // 蓝色幽冥色调
            Color netherColor = Color.Lerp(drawColor, new Color(100, 150, 255), 0.4f);
            
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2, 
                origin, NPC.scale, NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            
            return false;
        }
    }

    /// <summary>
    /// 幽冥龙头部 - 具备AI和攻击能力
    /// </summary>
    [AutoloadBossHead]
    public class NetherDragonHead : NetherDragon
    {
        public override WormType NPCWormType => WormType.Head;

        // AI状态
        private enum AIState
        {
            CircleAround,   // 环绕玩家
            Hover,          // 巡空
            Charge          // 冲刺
        }

        private AIState CurrentState
        {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // 幽冥火喷射计时器
        private int flameTimer = 0;
        private const int FlameInterval = 90; // 每1.5秒喷一次火

        // 状态计时器
        private int stateTimer = 0;

        public override void ChangeSummonType()
        {
            SummonNPCType = ModContent.NPCType<NetherDragonBody>();
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.height = 50;
            NPC.lifeMax = 120000;
            NPC.damage = 100;
            NPC.defense = 40;
        }

        public override void AI()
        {
            base.AI();

            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 初始化
            if (NPC.localAI[0] == 0f)
            {
                CurrentState = AIState.CircleAround;
                stateTimer = 300;
                flameTimer = FlameInterval;
                NPC.localAI[0] = 1f;
            }

            // 幽冥火喷射
            if (--flameTimer <= 0)
            {
                flameTimer = FlameInterval;
                ShootNetherFlames();
            }

            // AI状态机
            stateTimer--;
            switch (CurrentState)
            {
                case AIState.CircleAround:
                    CircleAroundMovement();
                    if (stateTimer <= 0)
                    {
                        CurrentState = AIState.Hover;
                        stateTimer = 240;
                    }
                    break;

                case AIState.Hover:
                    HoverMovement();
                    if (stateTimer <= 0)
                    {
                        CurrentState = AIState.Charge;
                        stateTimer = 60;
                    }
                    break;

                case AIState.Charge:
                    ChargeMovement();
                    if (stateTimer <= 0)
                    {
                        CurrentState = AIState.CircleAround;
                        stateTimer = 300;
                    }
                    break;
            }

            // 旋转和朝向
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }

        /// <summary>
        /// 环绕玩家移动
        /// </summary>
        private void CircleAroundMovement()
        {
            const float radius = 400f;
            const float speed = 0.05f;

            NPC.ai[1] += speed;
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 200f
            );

            Vector2 toTarget = targetPos - NPC.Center;
            const float inertia = 20f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 10f) / inertia;
        }

        /// <summary>
        /// 巡空移动
        /// </summary>
        private void HoverMovement()
        {
            const float hoverHeight = 350f;
            Vector2 targetPos = Target.Center - new Vector2(0, hoverHeight);

            // 随机左右飘移
            targetPos.X += MathF.Sin(NPC.ai[1] * 0.02f) * 200f;
            NPC.ai[1] += 1f;

            Vector2 toTarget = targetPos - NPC.Center;
            const float inertia = 30f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 15f) / inertia;
        }

        /// <summary>
        /// 冲刺攻击
        /// </summary>
        private void ChargeMovement()
        {
            if (stateTimer == 60)
            {
                // 冲刺开始，计算方向
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = toPlayer * 18f;
            }
            else if (stateTimer < 30)
            {
                // 减速
                NPC.velocity *= 0.95f;
            }
        }

        /// <summary>
        /// 喷射幽冥火
        /// </summary>
        private void ShootNetherFlames()
        {
            if (!NPC.HasValidTarget)
                return;

            int damage = 40;
            if (Main.expertMode)
                damage = 60;
            if (Main.masterMode)
                damage = 75;

            int count = 23;
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++)
            {
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-5f, 5f));
                Vector2 direction = toPlayer.RotatedBy(angleOffset);
                float speed = 10f + Main.rand.NextFloat(-3f, 3f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 40f,
                    direction * speed,
                    ModContent.ProjectileType<NetherFlameProjectile>(),
                    damage,
                    0f
                );
            }

            SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);
            Color netherColor = Color.Lerp(drawColor, new Color(100, 150, 255), 0.5f);

            // 绘制拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++)
            {
                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                float fade = 0.3f * (1f - i / (float)NPC.oldPos.Length);
                spriteBatch.Draw(tex, pos, null, netherColor * fade, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale, 
                    NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            }

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);

            return false;
        }
    }
}

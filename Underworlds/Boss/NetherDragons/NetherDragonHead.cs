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

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // 幽冥火喷射计时器
        private int flameTimer = 0;
        private const int FlameInterval = 90; // 每1.5秒喷一次火

        // 状态计时器
        private int stateTimer = 0;

        // 上一帧位置（用于绘制时的雾气效果）
        private Vector2 lastPosition = Vector2.Zero;

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<NetherDragonBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.height = 50;
            NPC.lifeMax = 120000;
            NPC.damage = 100;
            NPC.defense = 40;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);

            // 激活雾气系统
            if (Main.netMode != NetmodeID.Server) {
                NetherDragonFogSystem.Activate(NPC.whoAmI);
            }
        }

        public override void AI() {
            base.AI();

            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 初始化
            if (NPC.localAI[0] == 0f) {
                CurrentState = AIState.CircleAround;
                stateTimer = 300;
                flameTimer = FlameInterval;
                NPC.localAI[0] = 1f;
                lastPosition = NPC.Center;
            }

            // 幽冥火喷射
            if (--flameTimer <= 0) {
                flameTimer = FlameInterval;
                ShootNetherFlames();

                // 喷火时创建涟漪（表示能量爆发）
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.2f);
                }
            }

            // AI状态机
            stateTimer--;
            switch (CurrentState) {
                case AIState.CircleAround:
                    CircleAroundMovement();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.Hover;
                        stateTimer = 240;
                    }
                    break;

                case AIState.Hover:
                    HoverMovement();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.Charge;
                        stateTimer = 60;
                    }
                    break;

                case AIState.Charge:
                    ChargeMovement();
                    if (stateTimer <= 0) {
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

            lastPosition = NPC.Center;
        }

        /// <summary>
        /// 环绕玩家移动
        /// </summary>
        private void CircleAroundMovement() {
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
        private void HoverMovement() {
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
        private void ChargeMovement() {
            if (stateTimer == 60) {
                // 冲刺开始，计算方向
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = toPlayer * 18f;

                // 冲刺开始时创建冲击涟漪（表示蓄力爆发）
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.8f);
                }
            }
            else if (stateTimer < 30) {
                // 减速
                NPC.velocity *= 0.95f;
            }
        }

        /// <summary>
        /// 喷射幽冥火
        /// </summary>
        private void ShootNetherFlames() {
            if (!NPC.HasValidTarget)
                return;

            int damage = 40;
            if (Main.expertMode)
                damage = 60;
            if (Main.masterMode)
                damage = 75;

            int count = 23;
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++) {
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

        public override void OnKill() {
            base.OnKill();

            // 死亡时停用雾气系统，创建爆炸涟漪
            if (Main.netMode != NetmodeID.Server) {
                // 死亡大爆炸涟漪
                for (int i = 0; i < 3; i++) {
                    float delay = i * 0.1f;
                    Vector2 ripplePos = NPC.Center + Main.rand.NextVector2Circular(50f, 50f);
                    NetherDragonFogSystem.CreateRipple(ripplePos, 2.5f - delay);
                }

                NetherDragonFogSystem.Deactivate();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            // 根据雾气密度微调颜色（轻微效果）
            float fogDensity = 0f;
            if (Main.netMode != NetmodeID.Server && NetherDragonFogSystem.IsActive) {
                fogDensity = NetherDragonFogSystem.GetFogDensityAt(NPC.Center);
            }

            Color netherColor = Color.Lerp(drawColor, new Color(100, 150, 255), 0.5f);
            // 在浓雾中时颜色略微加深
            if (fogDensity > 0.6f) {
                netherColor = Color.Lerp(netherColor, new Color(80, 120, 200), fogDensity * 0.2f);
            }

            // 绘制拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++) {
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

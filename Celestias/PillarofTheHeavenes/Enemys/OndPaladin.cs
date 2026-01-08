using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Enemys
{
    /// <summary>
    /// 金甲圣骑 - 天柱区域的地面战士型敌怪
    /// 手持神圣长矛，能进行冲锋攻击和投掷光矛
    /// </summary>
    public class OndPaladin : ModNPC
    {
        #region 常量
        private const float WalkSpeed = 3f;
        private const float ChargeSpeed = 12f;
        private const float DetectionRange = 500f;
        #endregion

        #region 状态
        private enum AIState
        {
            Patrol,
            Chase,
            Attack,
            Charge,
            ThrowSpear
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float AttackCooldown => ref NPC.ai[2];
        private ref float ChargeDirection => ref NPC.ai[3];

        private float animationCounter = 0f;
        private int patrolDirection = 1;
        private bool isOnGround = false;
        private float armorGlowIntensity = 0f;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 56;
            NPC.damage = 95;
            NPC.defense = 55;
            NPC.lifeMax = 33500;
            NPC.knockBackResist = 0.1f;
            NPC.value = Item.buyPrice(gold: 3);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.lavaImmune = true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("金甲圣骑，天庭的精锐守卫。身披神铸金甲，手持圣光长矛，誓死守护天柱。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                State = AIState.Patrol;
                NPC.velocity.X *= 0.9f;
                return;
            }

            // 更新计时器
            StateTimer++;
            animationCounter++;
            if (AttackCooldown > 0) AttackCooldown--;

            // 检测是否在地面
            isOnGround = NPC.velocity.Y == 0 || NPC.collideY;

            // 更新朝向（冲锋时除外）
            if (State != AIState.Charge) {
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            }

            // 添加光照
            float glowPulse = 0.6f + MathF.Sin(animationCounter * 0.05f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.9f, 0.6f) * 0.4f * glowPulse);

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            switch (State) {
                case AIState.Patrol:
                    RunPatrolAI(target, distToTarget);
                    break;
                case AIState.Chase:
                    RunChaseAI(target, distToTarget);
                    break;
                case AIState.Attack:
                    RunAttackAI(target);
                    break;
                case AIState.Charge:
                    RunChargeAI(target);
                    break;
                case AIState.ThrowSpear:
                    RunThrowSpearAI(target);
                    break;
            }

            // 盔甲光芒强度
            float targetGlow = State == AIState.Charge ? 1f : (State == AIState.Attack ? 0.7f : 0.3f);
            armorGlowIntensity = MathHelper.Lerp(armorGlowIntensity, targetGlow, 0.1f);

            // 应用重力
            if (!NPC.noGravity && NPC.velocity.Y < 15f) {
                NPC.velocity.Y += 0.3f;
            }
        }

        private void RunPatrolAI(Player target, float distance) {
            // 巡逻移动
            NPC.velocity.X = patrolDirection * WalkSpeed * 0.5f;

            // 遇到墙壁或悬崖则转向
            if (NPC.collideX || !Collision.CanHitLine(NPC.Bottom, 1, 1, NPC.Bottom + new Vector2(patrolDirection * 32, 16), 1, 1)) {
                patrolDirection *= -1;
                NPC.spriteDirection = patrolDirection;
            }

            // 检测到玩家
            if (distance < DetectionRange && Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) {
                State = AIState.Chase;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);
            }
        }

        private void RunChaseAI(Player target, float distance) {
            // 追击玩家
            int direction = target.Center.X > NPC.Center.X ? 1 : -1;
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, direction * WalkSpeed, 0.1f);

            // 跳跃越障
            if (NPC.collideX && isOnGround) {
                NPC.velocity.Y = -8f;
            }

            // 近距离攻击
            if (distance < 80f && AttackCooldown <= 0) {
                State = AIState.Attack;
                StateTimer = 0;
            }
            // 中距离冲锋
            else if (distance > 150f && distance < 400f && AttackCooldown <= 0 && isOnGround && Main.rand.NextBool(120)) {
                State = AIState.Charge;
                StateTimer = 0;
                ChargeDirection = direction;
                NPC.spriteDirection = direction;
            }
            // 远距离投矛
            else if (distance > 250f && distance < 500f && AttackCooldown <= 0 && Main.rand.NextBool(90)) {
                State = AIState.ThrowSpear;
                StateTimer = 0;
            }

            // 目标丢失
            if (distance > DetectionRange * 1.5f) {
                State = AIState.Patrol;
                StateTimer = 0;
            }
        }

        private void RunAttackAI(Player target) {
            NPC.velocity.X *= 0.8f;

            if (StateTimer == 15) {
                // 挥击攻击
                SoundEngine.PlaySound(SoundID.Item1, NPC.Center);

                // 攻击粒子
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = new Vector2(NPC.spriteDirection * 4f, Main.rand.NextFloat(-2f, 2f));
                    int dust = Dust.NewDust(NPC.Center + new Vector2(NPC.spriteDirection * 30, 0), 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }

                // 近战伤害检测
                Rectangle hitbox = new Rectangle(
                    (int)NPC.Center.X + (NPC.spriteDirection == 1 ? 0 : -60),
                    (int)NPC.Center.Y - 30,
                    60, 60
                );

                if (target.Hitbox.Intersects(hitbox)) {
                    target.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage, NPC.spriteDirection);
                }
            }

            if (StateTimer > 40) {
                State = AIState.Chase;
                StateTimer = 0;
                AttackCooldown = 60;
            }
        }

        private void RunChargeAI(Player target) {
            if (StateTimer < 20) {
                // 蓄力阶段
                NPC.velocity.X *= 0.8f;

                // 蓄力粒子
                if (StateTimer % 3 == 0) {
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
                }
            }
            else if (StateTimer == 20) {
                // 开始冲锋
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
                NPC.velocity.X = ChargeDirection * ChargeSpeed;
            }
            else {
                // 冲锋中
                NPC.velocity.X = ChargeDirection * ChargeSpeed;

                // 冲锋粒子
                if (StateTimer % 2 == 0) {
                    int dust = Dust.NewDust(NPC.Bottom, NPC.width, 0, DustID.GoldFlame, -ChargeDirection * 3f, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }

                // 撞墙停止
                if (NPC.collideX) {
                    State = AIState.Chase;
                    StateTimer = 0;
                    AttackCooldown = 90;
                    NPC.velocity.X = 0;

                    // 撞击效果
                    for (int i = 0; i < 15; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                        Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.8f);
                    }
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f }, NPC.Center);
                }
            }

            if (StateTimer > 60) {
                State = AIState.Chase;
                StateTimer = 0;
                AttackCooldown = 120;
            }
        }

        private void RunThrowSpearAI(Player target) {
            NPC.velocity.X *= 0.9f;

            if (StateTimer == 25) {
                // 投掷光矛
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item39, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 spawnPos = NPC.Center + direction * 30f;

                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, direction * 14f,
                        ProjectileID.JavelinHostile, NPC.damage / 2, 3f, Main.myPlayer);
                }

                // 投掷光效
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = new Vector2(NPC.spriteDirection * 5f, Main.rand.NextFloat(-3f, 3f));
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 50) {
                State = AIState.Chase;
                StateTimer = 0;
                AttackCooldown = 100;
            }
        }

        public override void FindFrame(int frameHeight) {
            // 单帧图片
            NPC.frame.Y = 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos - new Vector2(0, 4);
            Vector2 origin = NPC.frame.Size() / 2;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 绘制主体
            spriteBatch.Draw(texture, drawPos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            // 绘制盔甲高光
            if (armorGlowIntensity > 0.2f) {
                Color glowColor = new Color(255, 240, 180, 0) * (armorGlowIntensity - 0.2f) * 0.6f;
                spriteBatch.Draw(texture, drawPos, NPC.frame, glowColor, NPC.rotation, origin, NPC.scale * 1.02f, effects, 0f);
            }

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, hit.HitDirection * 2, -1, 100, default, 1.2f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 35; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                    Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath14, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldBar, 2, 3, 6));
            npcLoot.Add(ItemDropRule.Common(ItemID.Javelin, 3, 10, 25));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofMight, 5, 1, 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }
}

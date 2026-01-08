using Microsoft.Xna.Framework;
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
    /// 天眼 - 天柱区域的悬浮观察者型敌怪
    /// 静静悬浮在空中，发现玩家后发射追踪激光
    /// </summary>
    public class HeavenObserver : ModNPC
    {
        #region 常量
        private const float HoverSpeed = 2f;
        private const float DetectionRange = 600f;
        private const float AttackRange = 500f;
        #endregion

        #region 状态
        private enum AIState
        {
            Idle,
            Alert,
            Attack,
            Reposition
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float HoverOffset => ref NPC.ai[2];
        private ref float AttackCooldown => ref NPC.ai[3];

        private float animationCounter = 0f;
        private float eyeGlowIntensity = 0f;
        private float pulseTimer = 0f;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 50;
            NPC.height = 50;
            NPC.damage = 70;
            NPC.defense = 35;
            NPC.lifeMax = 21800;
            NPC.knockBackResist = 0.1f;
            NPC.value = Item.buyPrice(gold: 1, silver: 50);
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath6;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.lavaImmune = true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("天眼，天庭的守望者。其金瞳洞察一切，能穿透尘世迷雾，发现任何入侵者。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity *= 0.95f;
                NPC.EncourageDespawn(120);
                return;
            }

            // 更新计时器
            StateTimer++;
            pulseTimer += 0.05f;
            animationCounter++;
            if (AttackCooldown > 0) AttackCooldown--;

            // 悬浮偏移
            HoverOffset = MathF.Sin(pulseTimer) * 20f;

            // 添加光照
            float glowPulse = 0.5f + MathF.Sin(pulseTimer * 2f) * 0.3f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.85f, 0.5f) * (0.6f + eyeGlowIntensity * 0.4f) * glowPulse);

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            switch (State) {
                case AIState.Idle:
                    RunIdleAI(target, distToTarget);
                    break;
                case AIState.Alert:
                    RunAlertAI(target, distToTarget);
                    break;
                case AIState.Attack:
                    RunAttackAI(target);
                    break;
                case AIState.Reposition:
                    RunRepositionAI(target, distToTarget);
                    break;
            }

            // 眼睛光芒强度插值
            float targetGlow = State == AIState.Attack ? 1f : (State == AIState.Alert ? 0.6f : 0.2f);
            eyeGlowIntensity = MathHelper.Lerp(eyeGlowIntensity, targetGlow, 0.1f);
        }

        private void RunIdleAI(Player target, float distance) {
            // 缓慢悬浮
            NPC.velocity.Y = MathF.Sin(pulseTimer * 0.5f) * 0.5f;
            NPC.velocity.X *= 0.95f;

            // 检测玩家
            if (distance < DetectionRange && Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) {
                State = AIState.Alert;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 0.6f }, NPC.Center);
            }
        }

        private void RunAlertAI(Player target, float distance) {
            // 面向玩家并准备攻击
            Vector2 toTarget = target.Center - NPC.Center;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, toTarget.ToRotation(), 0.1f);

            // 缓慢靠近攻击距离
            if (distance > AttackRange) {
                Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);
                NPC.velocity = Vector2.Lerp(NPC.velocity, dir * HoverSpeed, 0.05f);
            }
            else {
                NPC.velocity *= 0.9f;
            }

            // 警告粒子
            if (StateTimer % 10 == 0) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            // 进入攻击状态
            if (StateTimer > 60 && AttackCooldown <= 0 && distance <= AttackRange) {
                State = AIState.Attack;
                StateTimer = 0;
            }

            // 目标丢失
            if (distance > DetectionRange * 1.5f || !Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) {
                State = AIState.Idle;
                StateTimer = 0;
            }
        }

        private void RunAttackAI(Player target) {
            NPC.velocity *= 0.9f;

            // 蓄力阶段
            if (StateTimer < 40) {
                // 蓄力粒子效果
                if (StateTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (40 - StateTimer);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }
            else if (StateTimer == 40) {
                // 发射激光
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item33, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    
                    // 发射追踪弹
                    for (int i = 0; i < 5; i++) {
                        Vector2 vel = direction.RotatedBy((i - 2) * 0.1f) * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ProjectileID.LostSoulFriendly, NPC.damage / 2, 1f, Main.myPlayer);
                    }
                }

                // 发射光效
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 60) {
                State = AIState.Reposition;
                StateTimer = 0;
                AttackCooldown = 120;
            }
        }

        private void RunRepositionAI(Player target, float distance) {
            // 移动到新位置
            Vector2 targetPos = target.Center + Main.rand.NextVector2CircularEdge(AttackRange * 0.8f, AttackRange * 0.8f);
            Vector2 toPos = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toPos.SafeNormalize(Vector2.Zero) * HoverSpeed * 2f, 0.05f);

            if (StateTimer > 60) {
                State = AIState.Alert;
                StateTimer = 0;
            }
        }

        public override void FindFrame(int frameHeight) {
            // 单帧图片
            NPC.frame.Y = 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0, HoverOffset);
            Vector2 origin = NPC.frame.Size() / 2;

            // 绘制光晕
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex != null && eyeGlowIntensity > 0.1f) {
                Color glowColor = new Color(255, 220, 150, 0) * eyeGlowIntensity * 0.6f;
                spriteBatch.Draw(glowTex, drawPos, null, glowColor, 0f, glowTex.Size() / 2, 2f + eyeGlowIntensity, SpriteEffects.None, 0f);
            }

            // 绘制主体
            spriteBatch.Draw(texture, drawPos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);

            // 绘制高亮层
            if (eyeGlowIntensity > 0.3f) {
                Color highlightColor = new Color(255, 240, 200, 0) * (eyeGlowIntensity - 0.3f) * 0.5f;
                spriteBatch.Draw(texture, drawPos, NPC.frame, highlightColor, NPC.rotation, origin, NPC.scale * 1.05f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, hit.HitDirection * 2, -1, 100, default, 1.5f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 25; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                }
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofSight, 4, 1, 2));
            npcLoot.Add(ItemDropRule.Common(ItemID.LightShard, 5));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }
}

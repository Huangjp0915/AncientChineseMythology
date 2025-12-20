using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 枉死千骸 - 大后期Boss，由头部和两只手臂组成，类似骷髅王的多部件Boss
    /// </summary>
    [AutoloadBossHead]
    internal class Corpses : ModNPC
    {
        // ====== Boss阶段系统 ======
        public enum BossPhase
        {
            Intro,          // 出场
            BasicAttack,    // 基础攻击
            SpiralHunt,     // 螺旋追猎
            FuryCombo,      // 狂暴连击
            DarkRitual,     // 黑暗仪式
            FinalRage       // 终极狂暴
        }

        public BossPhase Phase
        {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float LocalTimer => ref NPC.ai[2];
        public ref float SpecialTimer => ref NPC.ai[3];

        // ====== Boss状态变量 ======
        private int seed = -1;
        private Random random;
        private bool spawnHands = true;
        private bool didIntroShock = false;
        private float introAppear = 0f;
        private float spiralAngle = 0f;
        private float spiralSpeed = 0f;
        private int comboCount = 0;
        private int comboMax = 3;

        // ====== 手臂引用 ======
        private CorpsesHand leftHand;
        private CorpsesHand rightHand;
        private bool handsInitialized = false;

        // ====== 网络同步数据 ======
        private readonly int[] otherAI = new int[4];

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 120;
            NPC.defense = 60;
            NPC.lifeMax = 800000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 100000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            LocalTimer = 0;
            introAppear = 0;

            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(introAppear);
            writer.Write(spiralAngle);
            writer.Write(spiralSpeed);
            writer.Write(comboCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            introAppear = reader.ReadSingle();
            spiralAngle = reader.ReadSingle();
            spiralSpeed = reader.ReadSingle();
            comboCount = reader.ReadInt32();
            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.8f);
        }

        public override void AI()
        {
            random ??= new Random(seed);

            // 生成手臂
            if (spawnHands)
            {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int leftHandIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, 
                        ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, -1);
                    int rightHandIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, 
                        ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, 1);
                }
            }

            // 初始化手臂引用
            if (!handsInitialized)
            {
                InitializeHandReferences();
            }

            // 目标选择
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead)
            {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead)
                {
                    NPC.velocity.Y -= 0.4f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            PhaseTimer++;
            LocalTimer++;

            // 根据阶段执行AI
            switch (Phase)
            {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.BasicAttack:
                    RunBasicAttack(target);
                    break;
                case BossPhase.SpiralHunt:
                    RunSpiralHunt(target);
                    break;
                case BossPhase.FuryCombo:
                    RunFuryCombo(target);
                    break;
                case BossPhase.DarkRitual:
                    RunDarkRitual(target);
                    break;
                case BossPhase.FinalRage:
                    RunFinalRage(target);
                    break;
            }
        }

        private void InitializeHandReferences()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.active && npc.ModNPC is CorpsesHand hand && npc.ai[0] == NPC.whoAmI)
                {
                    if (npc.ai[1] > 0)
                        rightHand = hand;
                    else if (npc.ai[1] < 0)
                        leftHand = hand;
                }
            }

            if (leftHand != null && rightHand != null)
                handsInitialized = true;
        }

        private void TransitionTo(BossPhase newPhase)
        {
            Phase = newPhase;
            PhaseTimer = 0;
            LocalTimer = 0;
            NPC.netUpdate = true;

            // 重置相关状态
            comboCount = 0;
            
            if (newPhase == BossPhase.SpiralHunt)
            {
                spiralAngle = 0;
                spiralSpeed = 0;
            }
        }

        // ====== AI阶段实现 ======
        private void RunIntro(Player target)
        {
            // 出场动画：从下方升起，带扭曲效果
            introAppear = ACMUtils.SineInOut(MathHelper.Clamp(PhaseTimer / 180f, 0, 1));
            
            Vector2 startPos = target.Center + new Vector2(0, 800);
            Vector2 endPos = target.Center + new Vector2(0, -250);
            Vector2 desired = Vector2.Lerp(startPos, endPos, introAppear);
            
            NPC.Center += (desired - NPC.Center) * 0.1f;
            NPC.velocity *= 0.85f;

            // 出场粒子效果
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 offset = Main.rand.NextVector2Circular(100, 100) * (1 - introAppear);
                    int dust = Dust.NewDust(NPC.Center + offset, 0, 0, DustID.Shadowflame, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -offset.SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            // 出场冲击
            if (!didIntroShock && introAppear > 0.95f)
            {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);
                
                // 屏幕震动
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(15, 50);

                // 爆发粒子
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int k = 0; k < 50; k++)
                    {
                        Vector2 vel = Main.rand.NextVector2Circular(15, 15);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Smoke, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 200)
            {
                TransitionTo(BossPhase.BasicAttack);
            }
        }

        private void RunBasicAttack(Player target)
        {
            // 基础逼近玩家
            Vector2 toTarget = target.Center - NPC.Center;
            float distance = toTarget.Length();
            Vector2 desiredVel = toTarget.SafeNormalize(Vector2.Zero) * 8f;
            
            // 添加侧向移动
            float sideOffset = MathF.Sin(PhaseTimer * 0.04f) * 5f;
            desiredVel = desiredVel.RotatedBy(sideOffset * 0.1f);

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.08f);

            // 近战攻击 - 更频繁
            if (PhaseTimer % 80 == 20 && handsInitialized)
            {
                // 交替让左右手挥砍
                if (Main.rand.NextBool())
                {
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.Slashing);
                }
                else
                {
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.Slashing);
                }
            }

            // 近战突刺攻击
            if (PhaseTimer % 80 == 50 && handsInitialized)
            {
                CorpsesHand attackHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (attackHand != null && attackHand.NPC.active)
                {
                    CommandHandAttack(attackHand, target, CorpsesHand.HandState.Reaching);
                }
            }

            // 骨头泼洒攻击 - 降低频率
            if (PhaseTimer % 200 == 100 && handsInitialized)
            {
                CorpsesHand tossHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (tossHand != null && tossHand.NPC.active)
                {
                    CommandHandAttack(tossHand, target, CorpsesHand.HandState.BoneToss);
                }
            }

            // 发射追踪暗影球 - 降低频率
            if (PhaseTimer % 120 == 60 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 spawnPos = NPC.Center + Main.rand.NextVector2Circular(50, 50);
                Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 6f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity,
                    ModContent.ProjectileType<CorpsesShadowOrb>(), GetBossDamage(0.7f), 2f);
            }

            // 双手拍掌攻击 - 降低频率，作为特殊技能
            if (PhaseTimer % 300 == 150 && handsInitialized && leftHand != null && rightHand != null)
            {
                // 确保两只手都处于空闲状态才发起拍掌
                if (leftHand.IsIdle() && rightHand.IsIdle())
                {
                    Vector2 clapCenter = target.Center + target.velocity * 20f;
                    // 增加蓄力距离 (80 -> 180)
                    Vector2 leftTarget = clapCenter + new Vector2(-180, 0);
                    Vector2 rightTarget = clapCenter + new Vector2(180, 0);
                    
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.ClapCharging, leftTarget);
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.ClapCharging, rightTarget);
                }
            }

            // 抓取攻击
            if (PhaseTimer % 150 == 75 && handsInitialized && distance < 300f)
            {
                CorpsesHand grabHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (grabHand != null && grabHand.NPC.active)
                {
                    CommandHandAttack(grabHand, target, CorpsesHand.HandState.Grabbing);
                }
            }

            // 生命值低于70%时转换阶段
            if (NPC.life < NPC.lifeMax * 0.7f)
            {
                TransitionTo(BossPhase.SpiralHunt);
            }
            else if (PhaseTimer > 600)
            {
                TransitionTo(BossPhase.SpiralHunt);
            }
        }

        // 指挥手臂攻击的辅助方法
        private void CommandHandAttack(CorpsesHand hand, Player target, CorpsesHand.HandState attackType, Vector2? customTarget = null)
        {
            if (hand == null || hand.NPC == null || !hand.NPC.active)
                return;

            // 调用手臂的公开攻击触发方法
            Vector2 targetPos = customTarget ?? target.Center;
            
            // 根据攻击类型预测玩家位置
            if (!customTarget.HasValue)
            {
                if (attackType == CorpsesHand.HandState.Slashing)
                {
                    targetPos = target.Center + target.velocity * 10f;
                }
                else if (attackType == CorpsesHand.HandState.Reaching)
                {
                    targetPos = target.Center + target.velocity * 15f;
                }
                else if (attackType == CorpsesHand.HandState.BoneToss)
                {
                    targetPos = target.Center + target.velocity * 12f;
                }
            }

            hand.TriggerAttack(attackType, targetPos);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // 这里可以添加Boss掉落物
            // npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SomeItem>()));
        }

        private void RunSpiralHunt(Player target)
        {
            // 改为固定在玩家上方悬停，不再环绕
            Vector2 hoverPos = target.Center + new Vector2(0, -400 + MathF.Sin(PhaseTimer * 0.05f) * 50f);
            NPC.Center += (hoverPos - NPC.Center) * 0.1f;
            NPC.velocity *= 0.9f;

            // 近战挥砍 - 更频繁
            if (PhaseTimer % 70 == 10 && handsInitialized)
            {
                if (leftHand != null && leftHand.NPC.active)
                {
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.Slashing);
                }
            }

            if (PhaseTimer % 70 == 40 && handsInitialized)
            {
                if (rightHand != null && rightHand.NPC.active)
                {
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.Slashing);
                }
            }

            // 骨头泼洒 - 偶尔使用
            if (PhaseTimer % 140 == 70 && handsInitialized)
            {
                CorpsesHand tossHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (tossHand != null && tossHand.NPC.active)
                {
                    CommandHandAttack(tossHand, target, CorpsesHand.HandState.BoneToss);
                }
            }

            // 发射暗影球弹幕 - 降低数量和频率
            if (PhaseTimer % 100 == 50 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 2; i++)
                {
                    float angle = MathHelper.Pi * i + PhaseTimer * 0.05f;
                    Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 80f;
                    Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 7f;
                    
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity,
                        ModContent.ProjectileType<CorpsesShadowOrb>(), GetBossDamage(0.75f), 2f);
                }
            }

            // 拍掌攻击 - 作为大招使用
            if (PhaseTimer % 250 == 125 && handsInitialized && leftHand != null && rightHand != null)
            {
                // 确保两只手都处于空闲状态才发起拍掌
                if (leftHand.IsIdle() && rightHand.IsIdle())
                {
                    Vector2 clapCenter = target.Center + target.velocity * 25f;
                    // 增加蓄力距离，使拍掌动作更大幅度 (80 -> 180)
                    Vector2 leftTarget = clapCenter + new Vector2(-180, 0);
                    Vector2 rightTarget = clapCenter + new Vector2(180, 0);
                    
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.ClapCharging, leftTarget);
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.ClapCharging, rightTarget);
                }
            }

            // 传送拍掌 - 新增的强力攻击
            if (PhaseTimer % 320 == 280 && handsInitialized && leftHand != null && rightHand != null)
            {
                if (leftHand.IsIdle() && rightHand.IsIdle())
                {
                    // 预测玩家位置
                    Vector2 predictPos = target.Center + target.velocity * 30f;
                    // 传送到玩家两侧更远的位置
                    Vector2 leftDest = predictPos + new Vector2(-250, -50);
                    Vector2 rightDest = predictPos + new Vector2(250, -50);
                    
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.TeleportClap, leftDest);
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.TeleportClap, rightDest);
                }
            }

            if (NPC.life < NPC.lifeMax * 0.4f || PhaseTimer > 500)
            {
                TransitionTo(BossPhase.FuryCombo);
            }
        }

        private void RunFuryCombo(Player target)
        {
            // 狂暴连击阶段
            if (comboCount < comboMax)
            {
                if (PhaseTimer % 80 == 0)
                {
                    // 冲向玩家
                    Vector2 dashDir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    NPC.velocity = dashDir * 25f;
                    comboCount++;

                    SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.2f }, NPC.Center);

                    // 冲刺时让手臂做出攻击姿态
                    if (handsInitialized)
                    {
                        if (comboCount % 2 == 0 && leftHand != null)
                        {
                            CommandHandAttack(leftHand, target, CorpsesHand.HandState.Slashing);
                        }
                        else if (rightHand != null)
                        {
                            CommandHandAttack(rightHand, target, CorpsesHand.HandState.Slashing);
                        }
                    }
                }

                if (PhaseTimer % 80 == 40)
                {
                    // 冲刺后的追击
                    if (handsInitialized)
                    {
                        if (comboCount % 2 == 1 && rightHand != null)
                        {
                            CommandHandAttack(rightHand, target, CorpsesHand.HandState.Reaching);
                        }
                        else if (leftHand != null)
                        {
                            CommandHandAttack(leftHand, target, CorpsesHand.HandState.Reaching);
                        }
                    }
                }
            }
            else
            {
                NPC.velocity *= 0.95f;
            }

            if (PhaseTimer > 300)
            {
                if (NPC.life < NPC.lifeMax * 0.2f)
                    TransitionTo(BossPhase.FinalRage);
                else
                    TransitionTo(BossPhase.BasicAttack);
            }
        }

        private void RunDarkRitual(Player target)
        {
            // 黑暗仪式 - 悬浮并施法
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.Center += (hoverPos - NPC.Center) * 0.08f;
            NPC.velocity *= 0.88f;

            // 让双手进行施法姿态
            if (PhaseTimer % 60 == 0 && handsInitialized)
            {
                if (leftHand != null)
                {
                    // 左手画圆
                    float circleAngle = PhaseTimer * 0.05f;
                    Vector2 circlePos = target.Center + new Vector2(MathF.Cos(circleAngle), MathF.Sin(circleAngle)) * 300f;
                    // 这里需要添加让手移动到指定位置的方法
                }

                if (rightHand != null)
                {
                    // 右手画圆（反向）
                    float circleAngle = -PhaseTimer * 0.05f + MathHelper.Pi;
                    Vector2 circlePos = target.Center + new Vector2(MathF.Cos(circleAngle), MathF.Sin(circleAngle)) * 300f;
                }
            }

            // 产生黑暗粒子
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer > 400)
            {
                TransitionTo(BossPhase.FuryCombo);
            }
        }

        private void RunFinalRage(Player target)
        {
            // 终极狂暴 - 更快更强
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVel = toTarget.SafeNormalize(Vector2.Zero) * 12f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.12f);

            // 狂暴状态下手臂攻击更频繁
            if (PhaseTimer % 45 == 0 && handsInitialized)
            {
                if (leftHand != null && leftHand.NPC.active)
                {
                    CommandHandAttack(leftHand, target, Main.rand.NextBool() ? 
                        CorpsesHand.HandState.Slashing : CorpsesHand.HandState.Grabbing);
                }
            }

            if (PhaseTimer % 45 == 22 && handsInitialized)
            {
                if (rightHand != null && rightHand.NPC.active)
                {
                    CommandHandAttack(rightHand, target, Main.rand.NextBool() ? 
                        CorpsesHand.HandState.Slashing : CorpsesHand.HandState.Reaching);
                }
            }

            // 产生愤怒粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
            {
                int dust = Dust.NewDust(NPC.Center, NPC.width, NPC.height, DustID.Torch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }

        public override bool CheckActive()
        {
            return false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = texture.Size() / 2;

            // 绘制拖尾
            float trailOpacity = 0.3f;
            for (int i = 0; i < NPC.oldPos.Length; i++)
            {
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                Color trailColor = Color.White * trailOpacity;
                trailOpacity *= 0.7f;

                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation, origin, NPC.scale * (0.9f + 0.1f * trailOpacity), SpriteEffects.None, 0);
            }

            // 绘制主体
            float scale = NPC.scale;
            if (Phase == BossPhase.Intro)
            {
                scale *= MathHelper.Lerp(0.7f, 1f, introAppear);
            }

            Color mainColor = drawColor;
            if (Phase == BossPhase.FinalRage)
            {
                mainColor = Color.Lerp(Color.White, Color.Red, 0.3f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f));
            }

            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition, null, mainColor, NPC.rotation, origin, scale, SpriteEffects.None, 0);

            return false;
        }

        public int GetBossDamage(float scaling = 1f)
        {
            return (int)(NPC.damage * scaling);
        }
    }
}

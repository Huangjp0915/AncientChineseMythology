using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 枉死千骸的手臂部件，使用IK系统实现自然的手臂运动
    /// </summary>
    internal class CorpsesHand : ModNPC
    {
        // ====== IK系统参数 ======
        private const float UpperArmLength = 120f;  // 上臂长度
        private const float ForearmLength = 100f;   // 前臂长度
        private const float MaxReach = UpperArmLength + ForearmLength - 10f; // 最大触及距离
        [VaultLoaden("AncientChineseMythology/Underworlds/Boss/Corpseses/")]
        private static Texture2D CorpsesArm = null;//反射加载得到手臂纹理，手臂宽26像素，高98像素

        // IK关节位置
        private Vector2 shoulderPos;  // 肩部（连接点）
        private Vector2 elbowPos;     // 肘部
        private Vector2 handPos;      // 手部

        // ====== 手部状态 ======
        public enum HandState
        {
            Idle,           // 空闲跟随
            Reaching,       // 伸展攻击
            Slashing,       // 挥砍
            Grabbing,       // 抓取
            Retracting      // 回缩
        }

        private HandState currentState = HandState.Idle;
        private int stateTimer = 0;
        private Vector2 targetPosition;
        private float attackProgress = 0f;

        // ====== 攻击和动作 ======
        private int attackCooldown = 0;
        private Vector2 slashStartPos;
        private Vector2 slashEndPos;
        private float slashProgress = 0f;
        
        // 抓取玩家相关
        private Player grabbedPlayer = null;
        private int grabDuration = 0;

        // ====== 拖尾效果 ======
        public List<Vector2> oldPositions = new List<Vector2>();
        public List<float> oldRotations = new List<float>();
        private const int TrailLength = 20;

        // ====== 方向标识 ======
        public int Direction
        {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        // ====== 公开的攻击触发接口 ======
        public void TriggerAttack(HandState attackType, Vector2? targetPos = null)
        {
            if (currentState == HandState.Idle || currentState == HandState.Retracting)
            {
                switch (attackType)
                {
                    case HandState.Reaching:
                        if (targetPos.HasValue)
                        {
                            targetPosition = targetPos.Value;
                            currentState = HandState.Reaching;
                            attackProgress = 0f;
                            stateTimer = 0;
                            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, NPC.Center);
                        }
                        break;

                    case HandState.Slashing:
                        if (targetPos.HasValue)
                        {
                            Vector2 dirToTarget = (targetPos.Value - NPC.Center).SafeNormalize(Vector2.UnitX);
                            slashStartPos = NPC.Center + dirToTarget.RotatedBy(Direction * -0.8f) * 200f;
                            slashEndPos = targetPos.Value + dirToTarget.RotatedBy(Direction * 0.8f) * 220f;
                            currentState = HandState.Slashing;
                            slashProgress = 0f;
                            stateTimer = 0;
                            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f }, NPC.Center);
                        }
                        break;

                    case HandState.Grabbing:
                        currentState = HandState.Grabbing;
                        stateTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 0.6f }, NPC.Center);
                        break;
                }

                attackCooldown = 60;
            }
        }

        public bool IsIdle()
        {
            return currentState == HandState.Idle;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.NPCBestiaryDrawModifiers npcDrawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers();
            npcDrawModifiers.Hide = true;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = npcDrawModifiers;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 80;
            NPC.defense = 40;
            NPC.lifeMax = 100000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 5000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override void AI()
        {
            // 获取Boss引用
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.active || boss.ModNPC is not Corpses corpsesBoss)
            {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            Player target = Main.player[boss.target];
            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;

            stateTimer++;
            if (attackCooldown > 0) attackCooldown--;

            // 根据状态执行不同的行为
            switch (currentState)
            {
                case HandState.Idle:
                    HandleIdleState(boss, target);
                    break;
                case HandState.Reaching:
                    HandleReachingState(boss, target);
                    break;
                case HandState.Slashing:
                    HandleSlashingState(boss, target);
                    break;
                case HandState.Grabbing:
                    HandleGrabbingState(boss, target);
                    break;
                case HandState.Retracting:
                    HandleRetractingState(boss, target);
                    break;
            }

            // 更新IK系统
            UpdateIKSystem(boss);

            // 更新拖尾
            UpdateTrail();

            // 处理抓取的玩家
            if (grabbedPlayer != null && grabDuration > 0)
            {
                grabbedPlayer.Center = NPC.Center;
                grabbedPlayer.velocity = Vector2.Zero;
                grabDuration--;

                if (grabDuration <= 0)
                {
                    // 抛出玩家
                    Vector2 throwDir = (boss.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    grabbedPlayer.velocity = throwDir * 20f;
                    grabbedPlayer = null;
                }
            }

            // 防止离Boss太远
            float distanceToBoss = Vector2.Distance(NPC.Center, boss.Center);
            if (distanceToBoss > 1200f)
            {
                NPC.Center = boss.Center + (NPC.Center - boss.Center).SafeNormalize(Vector2.Zero) * 1200f;
            }
        }

        // ====== IK系统核心 ======
        private void UpdateIKSystem(NPC boss)
        {
            // 肩部位置：在Boss身体两侧
            float shoulderOffset = Direction * 60f;
            shoulderPos = boss.Center + new Vector2(shoulderOffset, 30);

            // 目标位置（手部应该到达的位置）
            Vector2 targetPos = NPC.Center;
            
            // 计算从肩部到手部的向量
            Vector2 shoulderToHand = targetPos - shoulderPos;
            float distance = shoulderToHand.Length();

            // 如果距离超过最大触及距离，限制手部位置
            if (distance > MaxReach)
            {
                targetPos = shoulderPos + shoulderToHand.SafeNormalize(Vector2.Zero) * MaxReach;
                shoulderToHand = targetPos - shoulderPos;
                distance = MaxReach;
            }

            // 计算肘部位置（使用余弦定理）
            if (distance > 1f)
            {
                // 计算肘部弯曲角度
                float a = UpperArmLength;
                float b = ForearmLength;
                float c = distance;

                // 使用余弦定理计算肘部角度
                float angleA = MathF.Acos(MathHelper.Clamp((b * b + c * c - a * a) / (2 * b * c), -1f, 1f));
                
                // 肘部应该向外弯曲
                float baseAngle = shoulderToHand.ToRotation();
                float elbowAngle = baseAngle + MathHelper.PiOver2 * Direction;
                
                // 计算肘部偏移
                float elbowOffset = MathF.Sqrt(MathHelper.Max(0, a * a - (c * 0.5f) * (c * 0.5f)));
                Vector2 elbowDir = new Vector2(MathF.Cos(elbowAngle), MathF.Sin(elbowAngle));
                
                elbowPos = shoulderPos + shoulderToHand * 0.5f + elbowDir * elbowOffset * 0.5f;
            }
            else
            {
                elbowPos = shoulderPos;
            }

            handPos = targetPos;

            // 更新NPC旋转（手部朝向）
            Vector2 elbowToHand = handPos - elbowPos;
            if (elbowToHand.Length() > 1f)
            {
                NPC.rotation = elbowToHand.ToRotation();
            }
        }

        // ====== 状态处理 ======
        private void HandleIdleState(NPC boss, Player target)
        {
            // 空闲时跟随Boss，环绕移动
            float angle = Main.GameUpdateCount * 0.02f + (Direction > 0 ? 0 : MathHelper.Pi);
            float radius = 180f + MathF.Sin(Main.GameUpdateCount * 0.03f) * 30f;
            
            Vector2 desiredPos = boss.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            NPC.Center += (desiredPos - NPC.Center) * 0.15f;

            // 检查是否应该发起攻击
            if (attackCooldown <= 0 && stateTimer > 60)
            {
                float distToTarget = Vector2.Distance(NPC.Center, target.Center);
                
                if (distToTarget < 400f && Main.rand.NextBool(3))
                {
                    // 近距离抓取
                    StartGrabAttack(target);
                }
                else if (distToTarget < 600f && Main.rand.NextBool(2))
                {
                    // 中距离挥砍
                    StartSlashAttack(target);
                }
                else if (Main.rand.NextBool(4))
                {
                    // 伸展攻击
                    StartReachAttack(target);
                }
            }
        }

        private void HandleReachingState(NPC boss, Player target)
        {
            // 向目标位置伸展
            attackProgress = MathHelper.Clamp(attackProgress + 0.05f, 0, 1);
            float easeProgress = ACMUtils.QuadOut(attackProgress);

            Vector2 startPos = boss.Center + new Vector2(Direction * 120f, 0);
            NPC.Center = Vector2.Lerp(startPos, targetPosition, easeProgress);

            if (attackProgress >= 1f)
            {
                currentState = HandState.Retracting;
                attackProgress = 0f;
                stateTimer = 0;
            }
        }

        private void HandleSlashingState(NPC boss, Player target)
        {
            // 挥砍动作
            slashProgress = MathHelper.Clamp(slashProgress + 0.08f, 0, 1);
            float easeProgress = ACMUtils.SineInOut(slashProgress);

            NPC.Center = Vector2.Lerp(slashStartPos, slashEndPos, easeProgress);

            // 产生斩击粒子效果
            if (Main.netMode != NetmodeID.Server && slashProgress > 0.3f && slashProgress < 0.7f && Main.rand.NextBool(3))
            {
                int dust = Dust.NewDust(NPC.Center, NPC.width, NPC.height, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = NPC.rotation.ToRotationVector2() * 5f;
            }

            if (slashProgress >= 1f)
            {
                currentState = HandState.Retracting;
                slashProgress = 0f;
                stateTimer = 0;
                attackCooldown = 60;
            }
        }

        private void HandleGrabbingState(NPC boss, Player target)
        {
            // 抓取攻击
            Vector2 toTarget = target.Center - NPC.Center;
            float distance = toTarget.Length();

            if (distance > 5f)
            {
                NPC.velocity = toTarget.SafeNormalize(Vector2.Zero) * 18f;
            }
            else
            {
                NPC.velocity *= 0.8f;
            }

            // 检测是否抓到玩家
            if (stateTimer > 120)
            {
                currentState = HandState.Retracting;
                stateTimer = 0;
                attackCooldown = 90;
            }
        }

        private void HandleRetractingState(NPC boss, Player target)
        {
            // 回缩到Boss附近
            Vector2 restPos = boss.Center + new Vector2(Direction * 140f, -40f);
            NPC.Center += (restPos - NPC.Center) * 0.2f;

            if (Vector2.Distance(NPC.Center, restPos) < 30f || stateTimer > 40)
            {
                currentState = HandState.Idle;
                stateTimer = 0;
                attackProgress = 0f;
            }
        }

        // ====== 攻击发起方法 ======
        private void StartReachAttack(Player target)
        {
            currentState = HandState.Reaching;
            targetPosition = target.Center + target.velocity * 15f;
            attackProgress = 0f;
            stateTimer = 0;
            attackCooldown = 45;
            
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, NPC.Center);
        }

        private void StartSlashAttack(Player target)
        {
            currentState = HandState.Slashing;
            
            Vector2 dirToTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            slashStartPos = NPC.Center + dirToTarget.RotatedBy(Direction * -0.8f) * 200f;
            slashEndPos = target.Center + dirToTarget.RotatedBy(Direction * 0.8f) * 220f;
            
            slashProgress = 0f;
            stateTimer = 0;
            
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f }, NPC.Center);
        }

        private void StartGrabAttack(Player target)
        {
            currentState = HandState.Grabbing;
            stateTimer = 0;
            
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 0.6f }, NPC.Center);
        }

        private void UpdateTrail()
        {
            oldPositions.Add(NPC.Center);
            oldRotations.Add(NPC.rotation);

            if (oldPositions.Count > TrailLength)
            {
                oldPositions.RemoveAt(0);
                oldRotations.RemoveAt(0);
            }
        }

        public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox)
        {
            // 手部碰撞盒
            int hitboxSize = 60;
            npcHitbox = new Rectangle(
                (int)(NPC.Center.X - hitboxSize / 2),
                (int)(NPC.Center.Y - hitboxSize / 2),
                hitboxSize,
                hitboxSize
            );
            return true;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return currentState == HandState.Slashing || currentState == HandState.Reaching;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            // 抓取玩家
            if (currentState == HandState.Grabbing && grabbedPlayer == null)
            {
                grabbedPlayer = target;
                grabDuration = 60;
                SoundEngine.PlaySound(SoundID.NPCHit2, NPC.Center);
            }
        }

        public override bool CheckActive()
        {
            return false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D handTexture = TextureAssets.Npc[NPC.type].Value;
            Vector2 handOrigin = Direction > 0 ? new Vector2(0, handTexture.Height / 2) : new Vector2(handTexture.Width, handTexture.Height / 2);

            // 如果手臂纹理已加载，使用专门的手臂纹理绘制IK骨骼
            if (CorpsesArm != null && Main.netMode != NetmodeID.Server)
            {
                // 绘制上臂（从肩部到肘部）
                DrawArmSegment(spriteBatch, shoulderPos, elbowPos, CorpsesArm, drawColor, 1.0f);
                
                // 绘制前臂（从肘部到手部）
                DrawArmSegment(spriteBatch, elbowPos, handPos, CorpsesArm, drawColor, 0.9f);
            }
            else if (Main.netMode != NetmodeID.Server)
            {
                // 备用方案：使用简单的线条绘制骨骼（调试用）
                DrawBone(spriteBatch, shoulderPos, elbowPos, Color.Gray * 0.6f, 8f);
                DrawBone(spriteBatch, elbowPos, handPos, Color.Gray * 0.6f, 6f);
            }

            // 绘制手部拖尾效果
            float trailOpacity = 0.3f;
            for (int i = 0; i < oldPositions.Count; i++)
            {
                float progress = i / (float)oldPositions.Count;
                Vector2 drawPos = oldPositions[i] - Main.screenPosition;
                Color trailColor = drawColor * (trailOpacity * (1 - progress));
                float rot = oldRotations[i] + (Direction > 0 ? 0 : MathHelper.Pi);
                SpriteEffects effects = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(handTexture, drawPos, null, trailColor, rot, handOrigin, NPC.scale * 0.9f, effects, 0);
            }

            // 绘制主体手部
            float rotation = NPC.rotation + (Direction > 0 ? 0 : MathHelper.Pi);
            SpriteEffects mainEffects = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            
            Color mainColor = drawColor;
            if (currentState == HandState.Slashing && slashProgress > 0.3f && slashProgress < 0.7f)
            {
                // 攻击时发红光
                mainColor = Color.Lerp(drawColor, Color.Red, 0.4f);
            }

            spriteBatch.Draw(handTexture, NPC.Center - Main.screenPosition, null, mainColor, rotation, handOrigin, NPC.scale, mainEffects, 0);

            return false;
        }

        // 绘制手臂段（使用专门的手臂纹理）
        private void DrawArmSegment(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Texture2D armTexture, Color color, float scale = 1f)
        {
            Vector2 diff = end - start;
            float rotation = diff.ToRotation();
            float length = diff.Length();

            // 手臂纹理：宽26像素，高98像素
            // 计算需要绘制多少个手臂段来填充整个长度
            float armSegmentLength = armTexture.Height * scale;
            int segmentCount = (int)Math.Ceiling(length / armSegmentLength);
            
            // 从起点向终点绘制手臂段
            for (int i = 0; i < segmentCount; i++)
            {
                float progress = i / (float)segmentCount;
                Vector2 segmentPos = Vector2.Lerp(start, end, progress);
                
                // 计算这一段的实际长度
                float segmentLength = Math.Min(armSegmentLength, length - i * armSegmentLength);
                float lengthScale = segmentLength / armTexture.Height;
                
                // 纹理原点在底部中心，这样旋转时会围绕连接点旋转
                Vector2 origin = new Vector2(armTexture.Width * 0.5f, armTexture.Height);
                Vector2 drawScale = new Vector2(scale, lengthScale * scale);
                
                // 根据手臂状态调整颜色
                Color segmentColor = color;
                if (currentState == HandState.Slashing && slashProgress > 0.2f && slashProgress < 0.8f)
                {
                    // 攻击时手臂也发光
                    segmentColor = Color.Lerp(color, new Color(255, 100, 100), 0.3f * (float)Math.Sin(slashProgress * MathHelper.Pi));
                }
                else if (currentState == HandState.Reaching)
                {
                    // 伸展时淡淡的发光
                    segmentColor = Color.Lerp(color, new Color(200, 200, 255), 0.2f);
                }
                
                spriteBatch.Draw(
                    armTexture,
                    segmentPos - Main.screenPosition,
                    null,
                    segmentColor,
                    rotation + MathHelper.PiOver2, // 旋转90度使纹理正确朝向
                    origin,
                    drawScale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        // 绘制骨骼连接线（备用方案）
        private void DrawBone(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 diff = end - start;
            float rotation = diff.ToRotation();
            float length = diff.Length();

            // 使用简单的矩形绘制骨骼
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle(0, 0, 1, 1);
            Vector2 origin = new Vector2(0, 0.5f);
            Vector2 scale = new Vector2(length, thickness);

            spriteBatch.Draw(pixel, start - Main.screenPosition, rect, color, rotation, origin, scale, SpriteEffects.None, 0);
        }
    }
}

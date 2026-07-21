using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺爪臂段 — V3 差异化核心: 独立弹簧手势编排单元。
    /// 链式跟随与 Body 相同; 绘制层读取头部 <see cref="Aoshun.Gesture"/> 做弹簧姿态:
    /// 蓄势后仰(ReelBack) → 骤然挥出(Slash, poly8 急缓出) → 收拢(FoldIn) → 张臂(SpreadOut) → 震颤(Tremor)。
    /// 弹簧刻意欠阻尼 — 顿挫的机械感即角色感（MOTION §4: 不精确追踪 = 性格）。
    /// ai[1]: 前一段NPC索引  ai[2]: 臂序号(手势错相)  ai[3]: 头部NPC索引
    /// </summary>
    public class AoshunArms : ModNPC
    {
        // —— 弹簧姿态（纯视觉, 各端本地推进） ——
        private float armSwing;        // 当前挥角(rad, 相对身体法线)
        private float armSwingVel;
        private float armExtend;       // 当前伸展 0~1
        private float armExtendVel;
        private float slashFlash;      // 挥击瞬间残影强度

        private int ArmIndex => (int)NPC.ai[2];
        private int Side => ArmIndex % 2 == 0 ? 1 : -1; // 交替左右侧

        public override void SetDefaults() {
            NPC.width = 34;
            NPC.height = 32;
            NPC.damage = 20;
            NPC.defense = 25;
            NPC.lifeMax = 100000;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath60;
            NPC.behindTiles = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.noGravity = true;

            for (int k = 0; k < NPC.buffImmune.Length; k++) {
                NPC.buffImmune[k] = true;
            }
        }

        public override bool CheckActive() => false;

        private Aoshun Head {
            get {
                if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs &&
                    Main.npc[NPC.realLife].active && Main.npc[NPC.realLife].ModNPC is Aoshun head)
                    return head;
                return null;
            }
        }

        /// <summary>演出期间（入场/换阶段/死亡）无接触伤害</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            Aoshun head = Head;
            return head == null || head.ContactDamageEnabled;
        }

        public override bool PreAI() {
            if (NPC.ai[3] > 0)
                NPC.realLife = (int)NPC.ai[3];
            if (NPC.target < 0 || NPC.target == byte.MaxValue || Main.player[NPC.target].dead)
                NPC.TargetClosest(true);
            if (Main.player[NPC.target].dead)
                NPC.timeLeft = 50;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!Main.npc[(int)NPC.ai[1]].active) {
                    NPC.life = 0;
                    NPC.HitEffect(0, 10.0);
                    NPC.active = false;
                }
            }

            if (NPC.ai[1] < (double)Main.npc.Length) {
                Vector2 npcCenter = new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height * 0.5f);
                float dirX = Main.npc[(int)NPC.ai[1]].position.X + Main.npc[(int)NPC.ai[1]].width / 2 - npcCenter.X;
                float dirY = Main.npc[(int)NPC.ai[1]].position.Y + Main.npc[(int)NPC.ai[1]].height / 2 - npcCenter.Y;
                NPC.rotation = (float)Math.Atan2(dirY, dirX) + 1.57f;
                float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                float dist = (length - NPC.width) / length;
                NPC.position.X += dirX * dist;
                NPC.position.Y += dirY * dist;

                NPC.spriteDirection = dirX < 0f ? 1 : -1;
            }

            if (!Main.dedServ)
                UpdateGestureSpring();

            return false;
        }

        #region 弹簧手势（纯视觉）

        /// <summary>
        /// 根据头部手势解算目标姿态, 用刻意欠阻尼的弹簧追踪 —— 挥击的"甩"与收势的"余摆"由弹簧自然产生。
        /// </summary>
        private void UpdateGestureSpring() {
            Aoshun head = Head;
            float targetSwing = 0f;
            float targetExtend = 0f;
            float stiffness = 60f;
            float damping = 10f; // 欠阻尼(临界≈15.5) → 收势余摆

            if (head != null) {
                float t = head.GestureProgress(ArmIndex);
                switch (head.Gesture) {
                    case Aoshun.ArmGestureKind.ReelBack:
                        // 蓄势后仰: 二次缓动拉到反向 -0.95 rad, 微微收拢
                        targetSwing = -0.95f * AoshunHelper.QuadOut(t);
                        targetExtend = 0.45f;
                        stiffness = 30f; damping = 9f; // 慢而沉
                        break;
                    case Aoshun.ArmGestureKind.Slash:
                        // 挥斩: poly8 急缓出 → 几乎所有角位移在前几帧完成(斩击=一记重拍)
                        float snap = 1f - MathF.Pow(1f - t, 8f);
                        targetSwing = MathHelper.Lerp(-0.95f, 1.35f, snap);
                        targetExtend = 1f;
                        stiffness = 180f; damping = 12f; // 快而甩
                        if (t > 0.05f && t < 0.4f)
                            slashFlash = 1f;
                        break;
                    case Aoshun.ArmGestureKind.FoldIn:
                        targetSwing = -0.55f;
                        targetExtend = 0.8f * AoshunHelper.SineInOut(t);
                        stiffness = 45f; damping = 11f;
                        break;
                    case Aoshun.ArmGestureKind.SpreadOut:
                        targetSwing = 0.75f * AoshunHelper.SineInOut(t);
                        targetExtend = 1f;
                        stiffness = 50f; damping = 9f;
                        break;
                    case Aoshun.ArmGestureKind.Tremor:
                        // 震颤: 高频小幅抖动(相位按臂序号错开)
                        targetSwing = MathF.Sin(Main.GlobalTimeWrappedHourly * 34f + ArmIndex * 1.7f) * 0.22f;
                        targetExtend = 0.35f + MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + ArmIndex) * 0.12f;
                        stiffness = 120f; damping = 8f;
                        break;
                }
            }

            armSwing = ACMUtils.SpringDamp(armSwing, targetSwing, ref armSwingVel, stiffness, damping, 1f / 60f);
            armExtend = ACMUtils.SpringDamp(armExtend, targetExtend, ref armExtendVel, stiffness * 0.8f, damping, 1f / 60f);
            slashFlash *= 0.88f;

            // 挥击瞬间的风尘（速度门控: 只有真正甩起来才有）
            if (slashFlash > 0.55f && Main.rand.NextBool(2)) {
                Vector2 tip = ClawTipWorld();
                var d = Dust.NewDustPerfect(tip, DustID.Cloud);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = (tip - NPC.Center).SafeNormalize(Vector2.Zero) * 5f + Main.rand.NextVector2Circular(1.5f, 1.5f);
            }
        }

        /// <summary>身体法线方向（垂直于体轴, 按臂序号取交替侧）</summary>
        private Vector2 BodyNormal() {
            float bodyAngle = NPC.rotation - MathHelper.PiOver2;
            return (bodyAngle + MathHelper.PiOver2 * Side).ToRotationVector2();
        }

        /// <summary>爪尖世界坐标（手势姿态外推）</summary>
        private Vector2 ClawTipWorld() {
            Vector2 normal = BodyNormal().RotatedBy(armSwing * Side);
            return NPC.Center + normal * (10f + armExtend * 26f);
        }

        #endregion

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = NPC.spriteDirection == -1
                ? new Vector2(texture.Width * 0.5f, texture.Height * 0.5f)
                : new Vector2(texture.Width, texture.Height);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 姿态偏移: 沿法线外推 + 旋转偏转（绘制层, 不动碰撞链）
            Vector2 normal = BodyNormal();
            Vector2 poseOffset = normal.RotatedBy(armSwing * Side * 0.5f) * armExtend * 12f;
            float poseRotation = NPC.rotation + armSwing * Side * 0.55f;
            Vector2 drawPos = NPC.Center + poseOffset - Main.screenPosition;

            // 挥击残影: GlaciateWave 弧光沿挥向
            if (slashFlash > 0.1f && ACMAsset.GlaciateWave != null) {
                Texture2D wave = ACMAsset.GlaciateWave;
                Vector2 waveOrigin = wave.Size() / 2f;
                float swingDir = (NPC.rotation - MathHelper.PiOver2) + (MathHelper.PiOver2 + armSwing) * Side;
                Color arcColor = AoshunHelper.NorthSeaCyan * slashFlash * 0.55f;
                arcColor.A = 0;
                spriteBatch.Draw(wave, drawPos, null, arcColor, swingDir, waveOrigin,
                    new Vector2(0.16f, 0.05f) * (0.7f + slashFlash * 0.5f), SpriteEffects.None, 0f);
            }

            // 蓄势期的聚风提示: 后仰时爪侧微光
            if (armSwing < -0.35f && ACMAsset.SoftGlow != null) {
                float reel = MathHelper.Clamp(-armSwing / 0.95f, 0f, 1f);
                Color glow = AoshunHelper.LightningBlue * reel * 0.3f;
                glow.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, ClawTipWorld() - Main.screenPosition, null, glow,
                    0f, ACMAsset.SoftGlow.Size() / 2f, 0.32f * reel, SpriteEffects.None, 0f);
            }

            // 死亡演出: 整链渐隐白热（与 Body/Tail 一致）
            float deathProgress = Head?.DeathProgress ?? 0f;
            Color armColor = AoshunHelper.ApplyDeathFade(drawColor, deathProgress);

            spriteBatch.Draw(texture, drawPos, null, armColor,
                poseRotation, origin, NPC.scale, effects, 0);

            if (deathProgress > 0f && ACMAsset.SoftGlow != null) {
                Color white = AoshunHelper.ElectricWhite * deathProgress * 0.35f;
                white.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawPos, null, white, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            return false;
        }
    }
}

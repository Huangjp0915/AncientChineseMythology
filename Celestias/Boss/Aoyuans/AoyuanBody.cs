using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰身体段NPC - 链接到头部形成蠕虫结构
    /// 纹理AoyuanBody.png: 112×320, 5帧, 每帧112×64
    /// ai[0]: 段序号（0=颈部, 16=尾段; 死亡晶化从尾向头）
    /// ai[1]: 前一段NPC索引
    /// ai[2]: 当前段使用的帧号（0-4）
    /// ai[3]: 头部NPC索引（realLife指向）
    /// </summary>
    [AutoloadBossHead]
    public class AoyuanBody : ModNPC
    {
        private const int BodyFrameCount = 5;

        /// <summary>速度门控残影环形缓冲（本地视觉）</summary>
        private readonly Vector2[] ghostPos = new Vector2[4];
        private int ghostWriteIdx;
        private int ghostTick;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = BodyFrameCount;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 40;
            NPC.damage = 100;
            NPC.defense = 80;
            NPC.lifeMax = 430000;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.behindTiles = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.boss = false;
            NPC.dontCountMe = true;
            NPC.chaseable = false;
            NPC.alpha = 255;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            for (int k = 0; k < NPC.buffImmune.Length; k++) {
                NPC.buffImmune[k] = true;
            }
        }

        public override bool PreAI() {
            // 朝向前一段
            Vector2 chasePosition = Main.npc[(int)NPC.ai[1]].Center;
            Vector2 directionVector = chasePosition - NPC.Center;
            NPC.spriteDirection = directionVector.X > 0f ? 1 : -1;

            // 关联头部
            if (NPC.ai[3] > 0)
                NPC.realLife = (int)NPC.ai[3];

            Aoyuan head = Head;

            // 隐没同步（入场未现身/镜界入镜）: 保持全透明
            if (head is { BodyHidden: true }) {
                NPC.alpha = 255;
            }
            else {
                NPC.alpha -= 25;
                if (NPC.alpha < 0)
                    NPC.alpha = 0;
            }

            // 伤害窗口与头部对齐: 突刺帧满额, 静滞/巡逻擦身伤害, 演出零伤害
            if (head != null)
                NPC.damage = head.NPC.damage > 0 ? (int)(head.NPC.damage * 0.72f) : 0;

            // 隐没/死亡演出期间身体不可受击（避免锁血期偷伤害）
            NPC.dontTakeDamage = head != null
                && (head.BodyHidden || head.CurrentState == Aoyuan.AoyuanState.DeathAnim);

            // 目标选择
            if (NPC.target < 0 || NPC.target == byte.MaxValue || Main.player[NPC.target].dead)
                NPC.TargetClosest(true);
            if (Main.player[NPC.target].dead && NPC.timeLeft > 300)
                NPC.timeLeft = 300;

            // 检查头部存活
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!Main.npc[(int)NPC.ai[1]].active || Main.npc[(int)NPC.ai[3]].type != ModContent.NPCType<Aoyuan>()) {
                    NPC.life = 0;
                    NPC.HitEffect(0, 10.0);
                    NPC.active = false;
                    NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, NPC.whoAmI, -1f, 0f, 0f, 0, 0, 0);
                }
            }

            // 跟随前一段保持距离
            if (NPC.ai[1] < Main.npc.Length) {
                Vector2 npcCenter = new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height * 0.5f);
                float dirX = Main.npc[(int)NPC.ai[1]].position.X + Main.npc[(int)NPC.ai[1]].width / 2 - npcCenter.X;
                float dirY = Main.npc[(int)NPC.ai[1]].position.Y + Main.npc[(int)NPC.ai[1]].height / 2 - npcCenter.Y;
                NPC.rotation = (float)Math.Atan2(dirY, dirX) + 1.57f;
                float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                float dist = (length - NPC.width) / length;
                float posX = dirX * dist;
                float posY = dirY * dist;

                if (dirX < 0f)
                    NPC.spriteDirection = 1;
                else
                    NPC.spriteDirection = -1;

                NPC.velocity = Vector2.Zero;
                NPC.position.X += posX;
                NPC.position.Y += posY;
            }

            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                NPC.TargetClosest(true);

            // 残影环形缓冲（每 2 帧记录一次, 纯本地视觉）
            if (!Main.dedServ && ++ghostTick >= 2) {
                ghostTick = 0;
                ghostPos[ghostWriteIdx] = NPC.Center;
                ghostWriteIdx = (ghostWriteIdx + 1) % ghostPos.Length;
            }

            NPC.netUpdate = true;
            return false;
        }

        /// <summary>获取头部敖闰实例</summary>
        private Aoyuan Head {
            get {
                int idx = (int)NPC.ai[3];
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].ModNPC is Aoyuan a)
                    return a;
                return null;
            }
        }

        /// <summary>段序号（0=颈, 16=尾）</summary>
        public int SegmentIndex => (int)NPC.ai[0];

        /// <summary>当前身体段是否暴露冰晶弱点（绝对零度蓄力中）</summary>
        public bool WeakPointActive => Head is { WeakPointsExposed: true };

        /// <summary>死亡演出: 本段是否已晶化（尾→头顺序）</summary>
        public bool Crystallized => Head is { } h && h.CrystallizedSegments > 0
            && SegmentIndex >= Aoyuan.BodyFrameSequence.Length - h.CrystallizedSegments;

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            // 平时身体段高减伤（仅作护盾）；绝对零度蓄力时弱点暴露，可被有效击破
            modifiers.FinalDamage *= WeakPointActive ? 0.6f : 0.1f;
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) {
            AccumulateWeakPoint(damageDone);
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) {
            AccumulateWeakPoint(damageDone);
        }

        private void AccumulateWeakPoint(int dmg) {
            Aoyuan head = Head;
            if (head != null && head.WeakPointsExposed) {
                head.WeakPointDamageTaken += dmg;
                head.NPC.netUpdate = true;
            }
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            return false;
        }

        public override bool PreKill() {
            return false;
        }

        public override void FindFrame(int frameHeight) {
            NPC.frame.Y = frameHeight * (int)NPC.ai[2];
        }

        public override bool CheckActive() {
            if (NPC.AnyNPCs(ModContent.NPCType<Aoyuan>()))
                return false;
            NPC.active = false;
            return true;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life <= 0 && Main.netMode != NetmodeID.Server) {
                // 段消散冰爆（死亡连锁使用）
                AoyuanHelper.CreateIceBurst(NPC.Center, 90f, 2, 12);
                for (int i = 0; i < 10; i++) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20, 20),
                        Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.8f;
                    d.velocity = Main.rand.NextVector2Circular(5, 5);
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Aoyuan head = Head;

            // 隐没状态不绘制
            if (head is { BodyHidden: true })
                return false;

            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects fx = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = NPC.frame.Size() / 2f;

            // —— 速度门控残影: 头部突刺时全身留冰蓝重影 ——
            if (head != null && head.BladeActive) {
                for (int i = 0; i < ghostPos.Length; i++) {
                    if (ghostPos[i] == Vector2.Zero) continue;
                    float p = (i - ghostWriteIdx + ghostPos.Length) % ghostPos.Length / (float)ghostPos.Length;
                    Color gc = AoyuanHelper.FrostCyan * (0.30f * (1f - p));
                    gc.A = 0;
                    spriteBatch.Draw(texture, ghostPos[i] - screenPos, NPC.frame, gc, NPC.rotation, origin, NPC.scale, fx, 0f);
                }
            }

            // —— 死亡晶化: 从尾到头逐段白化 ——
            Color bodyColor = drawColor;
            bool crystal = Crystallized;
            if (crystal)
                bodyColor = Color.Lerp(drawColor, AoyuanHelper.IceCrystalWhite, 0.85f);

            spriteBatch.Draw(texture, NPC.Center - screenPos, NPC.frame, bodyColor, NPC.rotation,
                origin, NPC.scale, fx, 0f);

            // 晶化段的棱面星辉
            if (crystal && ACMAsset.Sparkle != null) {
                float tw = 0.5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + SegmentIndex * 1.7f) * 0.3f;
                Color sc = AoyuanHelper.IceCrystalWhite * (0.5f * tw);
                sc.A = 0;
                spriteBatch.Draw(ACMAsset.Sparkle, NPC.Center - screenPos, null, sc,
                    SegmentIndex * 0.8f, ACMAsset.Sparkle.Size() / 2f, 0.24f, SpriteEffects.None, 0f);
            }

            // —— P2 棱光冰甲: 每段低频冷辉（相位错开, 屏幕永不静止但不糊）——
            if (head is { IsPhase2: true } && !crystal && ACMAsset.Sparkle != null && SegmentIndex % 3 == 0) {
                float pulse = MathF.Max(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + SegmentIndex * 0.9f));
                Color gl = AoyuanHelper.FrostCyan * (0.22f * pulse * pulse);
                gl.A = 0;
                spriteBatch.Draw(ACMAsset.Sparkle, NPC.Center - screenPos, null, gl,
                    -Main.GlobalTimeWrappedHourly * 0.9f + SegmentIndex, ACMAsset.Sparkle.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            // —— 绝对零度弱点晶簇: 醒目可读的攻击点 ——
            if (WeakPointActive && ACMAsset.BlankStar != null) {
                Texture2D star = ACMAsset.BlankStar;
                float pulse = 0.6f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + NPC.whoAmI) * 0.4f;
                Color c = AoyuanHelper.IceCrystalWhite * pulse;
                c.A = 0;
                spriteBatch.Draw(star, NPC.Center - screenPos, null, c,
                    Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, 0.32f * pulse, SpriteEffects.None, 0f);
                // 弱点底座晶体
                AoyuanHelper.DrawCrystalShard(spriteBatch, NPC.Center, NPC.rotation, 1.1f,
                    AoyuanHelper.FrostCyan * 0.7f, 0.4f * pulse);
            }

            return false;
        }
    }
}

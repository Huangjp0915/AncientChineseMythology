using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰身体段NPC - 链接到头部形成蠕虫结构
    /// 纹理AoyuanBody.png: 112×320, 5帧, 每帧112×64
    /// ai[1]: 前一段NPC索引
    /// ai[2]: 当前段使用的帧号（0-4）
    /// ai[3]: 头部NPC索引（realLife指向）
    /// </summary>
    [AutoloadBossHead]
    public class AoyuanBody : ModNPC
    {
        private const int BodyFrameCount = 5;

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

            // 目标选择
            if (NPC.target < 0 || NPC.target == byte.MaxValue || Main.player[NPC.target].dead)
                NPC.TargetClosest(true);
            if (Main.player[NPC.target].dead && NPC.timeLeft > 300)
                NPC.timeLeft = 300;

            // 出生渐显粒子
            if (NPC.alpha != 0) {
                for (int spawnDust = 0; spawnDust < 2; spawnDust++) {
                    int d = Dust.NewDust(new Vector2(NPC.position.X, NPC.position.Y), NPC.width, NPC.height,
                        DustID.IceTorch, 0f, 0f, 100, default, 2f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].noLight = true;
                }
            }
            NPC.alpha -= 12;
            if (NPC.alpha < 0)
                NPC.alpha = 0;

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

            NPC.netUpdate = true;
            return false;
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 0.1f;
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

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            spriteBatch.Draw(texture, NPC.Center - screenPos, NPC.frame, drawColor, NPC.rotation,
                NPC.frame.Size() / 2, NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
            return false;
        }
    }
}

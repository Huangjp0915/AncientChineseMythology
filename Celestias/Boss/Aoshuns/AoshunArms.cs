using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺爪臂段NPC - 与Body交替排列形成蠕虫结构
    /// 纹理AoshunArms.png: 54×54, 单帧
    /// 参考AncientWyrmArms: 方向相关origin绘制，跟随前一段
    /// ai[1]: 前一段NPC索引
    /// ai[3]: 头部NPC索引（realLife指向）
    /// </summary>
    public class AoshunArms : ModNPC
    {
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

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = NPC.spriteDirection == -1
                ? new Vector2(texture.Width * 0.5f, texture.Height * 0.5f)
                : new Vector2(texture.Width, texture.Height);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition, null, drawColor,
                NPC.rotation, origin, NPC.scale, effects, 0);
            return false;
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
                float posX = dirX * dist;
                float posY = dirY * dist;

                if (dirX < 0f)
                    NPC.spriteDirection = 1;
                else
                    NPC.spriteDirection = -1;

                NPC.position.X += posX;
                NPC.position.Y += posY;
            }
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            return false;
        }
    }
}

using ElementsAwoken.Content.Buffs.Debuffs;
using ElementsAwoken.EASystem.EAPlayer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ElementsAwoken.Content.NPCs.Bosses.TheTempleKeepers
{
    public class AncientWyrmBody : ModNPC
    {
        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 32;
            NPC.damage = 50;
            NPC.defense = 45;
            NPC.lifeMax = 100000;
            NPC.knockBackResist = 0.0f;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath60;
            NPC.behindTiles = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.noGravity = true;
            for (int num2 = 0; num2 < 206; num2++)
            {
                NPC.buffImmune[num2] = true;
            }
            NPC.buffImmune[ModContent.BuffType<ExtinctionCurse>()] = true;
            NPC.buffImmune[ModContent.BuffType<IceBound>()] = true;
            NPC.buffImmune[ModContent.BuffType<EndlessTears>()] = true;
            NPC.GetGlobalNPC<AwakenedModeNPC>().dontExtraScale = true;
        }
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.damage = 75;
            if (MyWorld.awakenedMode)
            {
                NPC.damage = 90;
            }
        }
        public override bool CheckActive()
        {
            return false;
        }
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("ElementsAwoken/Content/NPCs/Bosses/TheTempleKeepers/Glow/AncientWyrmBody_Glow").Value;
            Rectangle frame = new Rectangle(0, texture.Height * NPC.frame.Y, texture.Width, texture.Height);
            Vector2 origin = NPC.spriteDirection == -1 ? (new Vector2(texture.Width * 0.5f, texture.Height * 0.5f)) : (new Vector2(texture.Width, texture.Height)); // to stop the sprite moving a lot when it turns
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition + new Vector2(0, NPC.gfxOffY), frame, new Color(255, 255, 255, 0), NPC.rotation, origin, NPC.scale, effects, 0.0f);
        }
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = NPC.spriteDirection == -1 ? (new Vector2(texture.Width * 0.5f, texture.Height * 0.5f)) : (new Vector2(texture.Width, texture.Height)); // to stop the sprite moving a lot when it turns
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            EAU.Sb.Draw(texture, NPC.Center - Main.screenPosition, new Rectangle?(), drawColor, NPC.rotation, origin, NPC.scale, effects, 0);
            return false;
        }
        public override bool PreAI()
        {
            bool expertMode = Main.expertMode;
            Player P = Main.player[NPC.target];
            if (NPC.ai[3] > 0)
                NPC.realLife = (int)NPC.ai[3];
            if (NPC.target < 0 || NPC.target == byte.MaxValue || Main.player[NPC.target].dead)
                NPC.TargetClosest(true);
            if (Main.player[NPC.target].dead)
                NPC.timeLeft = 50;
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (!Main.npc[(int)NPC.ai[1]].active)
                {
                    NPC.life = 0;
                    NPC.HitEffect(0, 10.0);
                    NPC.active = false;
                }
            }
            if (NPC.ai[1] < (double)Main.npc.Length)
            {
                Vector2 npcCenter = new Vector2(NPC.position.X + (float)NPC.width * 0.5f, NPC.position.Y + (float)NPC.height * 0.5f);
                float dirX = Main.npc[(int)NPC.ai[1]].position.X + (float)(Main.npc[(int)NPC.ai[1]].width / 2) - npcCenter.X;
                float dirY = Main.npc[(int)NPC.ai[1]].position.Y + (float)(Main.npc[(int)NPC.ai[1]].height / 2) - npcCenter.Y;
                NPC.rotation = (float)Math.Atan2(dirY, dirX) + 1.57f;
                float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                float dist = (length - (float)NPC.width) / length;
                float posX = dirX * dist;
                float posY = dirY * dist;
                if (dirX < 0f)
                {
                    NPC.spriteDirection = 1;

                }
                else
                {
                    NPC.spriteDirection = -1;
                }
                NPC.position.X = NPC.position.X + posX;
                NPC.position.Y = NPC.position.Y + posY;
            }
            return false;
        }
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
        {
            return false;
        }
    }
}
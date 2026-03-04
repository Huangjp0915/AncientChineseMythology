using ElementsAwoken.Content.Buffs.Debuffs;
using ElementsAwoken.Content.Items.BossDrops.TheTempleKeepers;
using ElementsAwoken.Content.Projectiles.NPCProj;
using ElementsAwoken.EASystem.EAPlayer;
using ElementsAwoken.EASystem.Loot;
using ElementsAwoken.EAUtilities;
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

namespace ElementsAwoken.Content.NPCs.Bosses.TheTempleKeepers
{
    [AutoloadBossHead]
    public class AncientWyrmHead : ModNPC
    {
        public int halfLife = 0;
        public int dayTime = 0;
        public bool despawn = false;
        public bool close = false;
        public bool chargePlayer = false;
        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 28;
            NPC.lifeMax = 200000;
            NPC.damage = 150;
            NPC.defense = 45;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.netAlways = true;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath60;
            NPC.value = Item.buyPrice(0, 20, 0, 0);
            NPC.npcSlots = 1f;
            Music = MusicID.GoblinInvasion;
            for (int num2 = 0; num2 < 206; num2++)
            {
                NPC.buffImmune[num2] = true;
            }
            NPC.buffImmune[ModContent.BuffType<IceBound>()] = true;
            NPC.buffImmune[ModContent.BuffType<EndlessTears>()] = true;
            NPC.GetGlobalNPC<AwakenedModeNPC>().dontExtraScale = true;
        }
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 2;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Scale = 0.9f,
                PortraitScale = null,
                CustomTexturePath = "ElementsAwoken/Extra/Bestiary/AncientWyrmBestiary"
            };
            value.Position.X += 20f;
            value.Position.Y += 5f;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }
        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(
            [
                new FlavorTextBestiaryInfoElement("Mods.ElementsAwoken.Bestiary.Bosses.AncientWyrm"),
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
            ]);
        }
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)EAU.BalanceHP(150000, balance, bossAdjustment, 250000);
            NPC.damage = (int)EAU.BalanceDamage(150, balance, bossAdjustment, 250);
            NPC.defense = EAU.BalanceDefense(45, 60);
        }
        public override bool CheckActive()
        {
            return false;
        }
        public override void FindFrame(int frameHeight)
        {
            if (close)
            {
                NPC.frame.Y = 1;
            }
            else
            {
                NPC.frame.Y = 0;
            }
        }
        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.GreaterHealingPotion;
        }
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            var AncientWyrm = new LeadingConditionRule(new EAIDRC.DropTheEyeDeath());

            AncientWyrm.OnSuccess(ItemDropRule.ByCondition(new Conditions.IsMasterMode(), ModContent.ItemType<AncientWyrmRelicItem>()));

            AncientWyrm.OnSuccess(ItemDropRule.Common(ModContent.ItemType<WyrmHeart>()));

            AncientWyrm.OnSuccess(ItemDropRule.OneFromOptions(1, [.. EAList.TempLoot]));
            AncientWyrm.OnSuccess(ItemDropRule.ByCondition(new Conditions.IsExpert(), ModContent.ItemType<TempleKeepersBag>(), 1));
            npcLoot.Add(AncientWyrm);
        }
        public override void OnKill()
        {
            MyWorld.downedAncientWyrm = true;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData); // Immediately inform clients of new world state.
        }
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("ElementsAwoken/Content/NPCs/Bosses/TheTempleKeepers/Glow/AncientWyrmHead_Glow").Value;
            Rectangle frame = new Rectangle(0, texture.Height * NPC.frame.Y, texture.Width, texture.Height);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            int num = texture.Height / 2;
            int yPos = num * NPC.frame.Y;
            Rectangle sourceRectangle = new Rectangle(0, yPos, texture.Width, num);
            Vector2 origin = NPC.spriteDirection == -1 ? (new Vector2(texture.Width * 0.5f + 10, num * 0.5f + 16)) : (new Vector2(texture.Width - 10, num + 16)); // to stop the sprite moving a lot when it turns
            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition + new Vector2(0, NPC.gfxOffY), NPC.frame, new Color(255, 255, 255, 0), NPC.rotation, origin, NPC.scale, effects, 0.0f);
        }
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            int num = texture.Height / 2;
            int yPos = num * NPC.frame.Y;
            Rectangle sourceRectangle = new Rectangle(0, yPos, texture.Width, num);
            Vector2 origin = NPC.spriteDirection == -1 ? (new Vector2(texture.Width * 0.5f + 10, num * 0.5f + 16)) : (new Vector2(texture.Width - 10, num + 16)); // to stop the sprite moving a lot when it turns
            EAU.Sb.Draw(texture, NPC.Center - Main.screenPosition, sourceRectangle, drawColor, NPC.rotation, origin, NPC.scale, effects, 0);
            return false;
        }
        public override bool PreAI()
        {
            Player P = Main.player[NPC.target];
            bool expertMode = Main.expertMode;
            if (Main.player[NPC.target].dead || Main.dayTime)
            {
                despawn = true;
            }
            if (despawn)
            {
                NPC.velocity.Y = NPC.velocity.Y + 0.11f;
                NPC.ai[1]++;
                if (NPC.ai[1] >= 300)
                {
                    NPC.active = false;
                }
            }
            if (Vector2.Distance(NPC.Center, P.Center) <= 400)
            {
                close = true;
            }
            else
            {
                close = false;
            }
            if (close)
            {
                Vector2 vector8 = new Vector2(NPC.position.X + (NPC.width / 2), NPC.position.Y + (NPC.height / 2));
                if (Main.rand.Next(7) == 0)
                {
                    int damage = Main.expertMode ? 35 : 50; // reduce damage in expert mode because it doubles anyway
                    Projectile.NewProjectile(EAU.NPCs(NPC), vector8.X + NPC.velocity.X, vector8.Y + NPC.velocity.Y, NPC.velocity.X * 0.8f + Main.rand.NextFloat(-0.7f, 0.7f) * 3, NPC.velocity.Y * 0.8f + Main.rand.NextFloat(-0.7f, 0.7f) * 3, ModContent.ProjectileType<WyrmBreath>(), damage, 0f, Main.myPlayer);
                    if (Main.rand.Next(2) == 0)
                    {
                        SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath, NPC.position);
                    }
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (NPC.ai[0] == 0)
                {
                    NPC.realLife = NPC.whoAmI;
                    int latestNPC = NPC.whoAmI;
                    int randomWormLength = Main.rand.Next(30, 40);
                    for (int i = 0; i < randomWormLength; i++)
                    {
                        int bodyType = ModContent.NPCType<AncientWyrmBody>();
                        if (i % 2 == 0)
                        {
                            bodyType = ModContent.NPCType<AncientWyrmArms>();
                        }
                        else
                        {
                            ModContent.NPCType<AncientWyrmBody>();
                        }
                        latestNPC = NPC.NewNPC(EAU.NPCs(NPC), (int)NPC.position.X + (NPC.width / 2), (int)NPC.position.Y + (NPC.height / 2), bodyType, NPC.whoAmI, 0, latestNPC);                   
                        Main.npc[(int)latestNPC].realLife = NPC.whoAmI;
                        Main.npc[(int)latestNPC].ai[3] = NPC.whoAmI;
                    }
                    NPC.NewNPC(EAU.NPCs(NPC), (int)NPC.position.X + (NPC.width / 2), (int)NPC.position.Y + (NPC.height / 2), ModContent.NPCType<AncientWyrmTail>(), NPC.whoAmI, 0, latestNPC);
                    Main.npc[(int)latestNPC].realLife = NPC.whoAmI;
                    Main.npc[(int)latestNPC].ai[3] = NPC.whoAmI;

                    NPC.ai[0] = 1;
                    NPC.netUpdate = true;
                }
            }
            int minTilePosX = (int)(NPC.position.X / 16.0) - 1;
            int maxTilePosX = (int)((NPC.position.X + NPC.width) / 16.0) + 2;
            int minTilePosY = (int)(NPC.position.Y / 16.0) - 1;
            int maxTilePosY = (int)((NPC.position.Y + NPC.height) / 16.0) + 2;
            if (minTilePosX < 0)
                minTilePosX = 0;
            if (maxTilePosX > Main.maxTilesX)
                maxTilePosX = Main.maxTilesX;
            if (minTilePosY < 0)
                minTilePosY = 0;
            if (maxTilePosY > Main.maxTilesY)
                maxTilePosY = Main.maxTilesY;

            bool collision = false;
            for (int i = minTilePosX; i < maxTilePosX; ++i)
            {
                for (int j = minTilePosY; j < maxTilePosY; ++j)
                {
                    if (Main.tile[i, j] != null && (Main.tile[i, j].HasUnactuatedTile && (Main.tileSolid[(int)Main.tile[i, j].TileType] || Main.tileSolidTop[(int)Main.tile[i, j].TileType] && (int)Main.tile[i, j].TileFrameY == 0) || (int)Main.tile[i, j].LiquidAmount > 64))
                    {
                        Vector2 vector2;
                        vector2.X = (float)(i * 16);
                        vector2.Y = (float)(j * 16);
                        if (NPC.position.X + NPC.width > vector2.X && NPC.position.X < vector2.X + 16.0 && (NPC.position.Y + NPC.height > (double)vector2.Y && NPC.position.Y < vector2.Y + 16.0))
                        {
                            collision = true;
                            if (Main.rand.Next(100) == 0 && Main.tile[i, j].HasUnactuatedTile)
                                WorldGen.KillTile(i, j, true, true, false);
                        }
                    }
                }
            }
            if (Vector2.Distance(NPC.Center, P.Center) >= 500)
            {
                chargePlayer = true;
            }
            if (Vector2.Distance(NPC.Center, P.Center) <= 350)           
            {
                chargePlayer = false;
            }
            float speed = 18f;
            if (!NPC.AnyNPCs(Mod.Find<ModNPC>("TheEye").Type))
            {
                speed = 25f;
            }

            float acceleration = 0.6f;

            Vector2 npcCenter = new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height * 0.5f);
            float targetXPos = Main.player[NPC.target].position.X + (Main.player[NPC.target].width / 2);
            float targetYPos = Main.player[NPC.target].position.Y + (Main.player[NPC.target].height / 2);

            float targetRoundedPosX = (float)((int)(targetXPos / 16.0) * 16);
            float targetRoundedPosY = (float)((int)(targetYPos / 16.0) * 16);
            npcCenter.X = (float)((int)(npcCenter.X / 16.0) * 16);
            npcCenter.Y = (float)((int)(npcCenter.Y / 16.0) * 16);
            float dirX = targetRoundedPosX - npcCenter.X;
            float dirY = targetRoundedPosY - npcCenter.Y;
            Vector2 vector168 = NPC.Center;
            float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (!collision)
            {
                NPC.TargetClosest(true);
                NPC.velocity.Y = NPC.velocity.Y + 0.11f;
                if (NPC.velocity.Y > speed)
                    NPC.velocity.Y = speed;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.4)
                {
                    if (NPC.velocity.X < 0.0)
                        NPC.velocity.X = NPC.velocity.X - acceleration * 1.1f;
                    else
                        NPC.velocity.X = NPC.velocity.X + acceleration * 1.1f;
                }
                else if (NPC.velocity.Y == speed)
                {
                    if (NPC.velocity.X < dirX)
                        NPC.velocity.X = NPC.velocity.X + acceleration;
                    else if (NPC.velocity.X > dirX)
                        NPC.velocity.X = NPC.velocity.X - acceleration;
                }
                else if (NPC.velocity.Y > 4.0)
                {
                    if (NPC.velocity.X < 0.0)
                        NPC.velocity.X = NPC.velocity.X + acceleration * 0.9f;
                    else
                        NPC.velocity.X = NPC.velocity.X - acceleration * 0.9f;
                }
            }
            if (collision || chargePlayer)
            {
                if (NPC.soundDelay == 0)
                {
                    float num1 = length / 40f;
                    if (num1 < 10.0)
                        num1 = 10f;
                    if (num1 > 20.0)
                        num1 = 20f;
                    NPC.soundDelay = (int)num1;
                    SoundEngine.PlaySound(SoundID.WormDig, NPC.position);
                }
                float absDirX = Math.Abs(dirX);
                float absDirY = Math.Abs(dirY);
                float newSpeed = speed / length;
                dirX = dirX * newSpeed;
                dirY = dirY * newSpeed;
                if (NPC.velocity.X > 0.0 && dirX > 0.0 || NPC.velocity.X < 0.0 && dirX < 0.0 || (NPC.velocity.Y > 0.0 && dirY > 0.0 || NPC.velocity.Y < 0.0 && dirY < 0.0))
                {
                    if (NPC.velocity.X < dirX)
                        NPC.velocity.X = NPC.velocity.X + acceleration;
                    else if (NPC.velocity.X > dirX)
                        NPC.velocity.X = NPC.velocity.X - acceleration;
                    if (NPC.velocity.Y < dirY)
                        NPC.velocity.Y = NPC.velocity.Y + acceleration;
                    else if (NPC.velocity.Y > dirY)
                        NPC.velocity.Y = NPC.velocity.Y - acceleration;
                    if (Math.Abs(dirY) < speed * 0.2 && (NPC.velocity.X > 0.0 && dirX < 0.0 || NPC.velocity.X < 0.0 && dirX > 0.0))
                    {
                        if (NPC.velocity.Y > 0.0)
                            NPC.velocity.Y = NPC.velocity.Y + acceleration * 2f;
                        else
                            NPC.velocity.Y = NPC.velocity.Y - acceleration * 2f;
                    }
                    if (Math.Abs(dirX) < speed * 0.2 && (NPC.velocity.Y > 0.0 && dirY < 0.0 || NPC.velocity.Y < 0.0 && dirY > 0.0))
                    {
                        if (NPC.velocity.X > 0.0)
                            NPC.velocity.X = NPC.velocity.X + acceleration * 2f;
                        else
                            NPC.velocity.X = NPC.velocity.X - acceleration * 2f;
                    }
                }
                else if (absDirX > absDirY)
                {
                    if (NPC.velocity.X < dirX)
                        NPC.velocity.X = NPC.velocity.X + acceleration * 1.1f;
                    else if (NPC.velocity.X > dirX)
                        NPC.velocity.X = NPC.velocity.X - acceleration * 1.1f;
                    if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5)
                    {
                        if (NPC.velocity.Y > 0.0)
                            NPC.velocity.Y = NPC.velocity.Y + acceleration;
                        else
                            NPC.velocity.Y = NPC.velocity.Y - acceleration;
                    }
                }
                else
                {
                    if (NPC.velocity.Y < dirY)
                        NPC.velocity.Y = NPC.velocity.Y + acceleration * 1.1f;
                    else if (NPC.velocity.Y > dirY)
                        NPC.velocity.Y = NPC.velocity.Y - acceleration * 1.1f;
                    if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5)
                    {
                        if (NPC.velocity.X > 0.0)
                            NPC.velocity.X = NPC.velocity.X + acceleration;
                        else
                            NPC.velocity.X = NPC.velocity.X - acceleration;
                    }
                }
            }

            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;
            if (NPC.velocity.X < 0f)
            {
                NPC.spriteDirection = 1;

            }
            else
            {
                NPC.spriteDirection = -1;
            }
            if (collision)
            {
                if (NPC.localAI[0] != 1)
                    NPC.netUpdate = true;
                NPC.localAI[0] = 1f;
            }
            else
            {
                if (NPC.localAI[0] != 0.0)
                    NPC.netUpdate = true;
                NPC.localAI[0] = 0.0f;
            }
            if ((NPC.velocity.X > 0.0 && NPC.oldVelocity.X < 0.0 || NPC.velocity.X < 0.0 && NPC.oldVelocity.X > 0.0 || (NPC.velocity.Y > 0.0 && NPC.oldVelocity.Y < 0.0 || NPC.velocity.Y < 0.0 && NPC.oldVelocity.Y > 0.0)) && !NPC.justHit)
                NPC.netUpdate = true;

            return false;
        }
        public override void BossHeadRotation(ref float rotation)
        {
            rotation = NPC.rotation;
        }
        public override void BossHeadSpriteEffects(ref SpriteEffects spriteEffects)
        {
            spriteEffects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        }
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
        {
            scale = 1.2f;   
            return null;
        }
    }
}
using ElementsAwoken.Content.Buffs.Debuffs;
using ElementsAwoken.Content.Items.BossDrops.TheTempleKeepers;
using ElementsAwoken.Content.Projectiles.NPCProj.TheGuardian;
using ElementsAwoken.EASystem.Loot;
using ElementsAwoken.EAUtilities;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ElementsAwoken.Content.NPCs.Bosses.TheTempleKeepers
{
    [AutoloadBossHead]
    public class TheEye : ModNPC
    {
        int projectileBaseDamage = 0;

        public override void SetDefaults()
        {
            NPC.width = 66;
            NPC.height = 52;
            NPC.lifeMax = 35000;
            NPC.damage = 0;
            NPC.defense = 40;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.netAlways = true;
            NPC.scale = 1.1f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath8;
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            Music = MusicID.GoblinInvasion;
            for (int num2 = 0; num2 < 206; num2++)
            {
                NPC.buffImmune[num2] = true;
            }
            NPC.buffImmune[ModContent.BuffType<IceBound>()] = true;
            NPC.buffImmune[ModContent.BuffType<EndlessTears>()] = true;
        }
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 2;
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Velocity = -1f,
                Direction = -1
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }
        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                new FlavorTextBestiaryInfoElement("Mods.ElementsAwoken.Bestiary.Bosses.TheEye"),
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
            });
        }
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            var AncientWyrm = new LeadingConditionRule(new EAIDRC.DropAncientWyrmHeadDeath());

            AncientWyrm.OnSuccess(ItemDropRule.ByCondition(new Conditions.IsMasterMode(), ModContent.ItemType<TheEyeRelicItem>()));

            AncientWyrm.OnSuccess(ItemDropRule.Common(ModContent.ItemType<TempleFragment>()));

            AncientWyrm.OnSuccess(ItemDropRule.OneFromOptions(1, [.. EAList.TempLoot]));
            AncientWyrm.OnSuccess(ItemDropRule.ByCondition(new Conditions.IsExpert(), ModContent.ItemType<TempleKeepersBag>(), 1));
            npcLoot.Add(AncientWyrm);
        }
        public override void OnKill()
        {
            MyWorld.downedEye = true;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        }
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = (int)EAU.BalanceHP(25000, balance, bossAdjustment, 50000);
            NPC.defense = EAU.BalanceDefense(40, 50);
        }
        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.GreaterHealingPotion;
        }
        public override void HitEffect(NPC.HitInfo hit)
        {
            if (NPC.life <= 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    Gore.NewGore(EAU.NPCs(NPC), NPC.position, NPC.velocity, Mod.Find<ModGore>("TheEye" + i).Type, 1f);
                }
            }
        }
        public override void FindFrame(int frameHeight)
        {
            NPC.frameCounter += 1;
            if (NPC.life > NPC.lifeMax * 0.75f)
            {
                NPC.frame.Y = 1 * frameHeight;
            }
            else
            {
                NPC.frame.Y = 0 * frameHeight;
            }
        }
        public override void AI()
        {
            Player P = Main.player[NPC.target];
            if (Main.masterMode)
            {
                if (MyWorld.awakenedMode) projectileBaseDamage = 9;
                else projectileBaseDamage = 8;
            }
            if (Main.expertMode)
            {
                if (MyWorld.awakenedMode) projectileBaseDamage = 29;
                else projectileBaseDamage = 27;
            }
            else projectileBaseDamage = 37;
            #region despawning
            if (!P.active || P.dead)
            {
                NPC.TargetClosest(true);
                if (!P.active || P.dead)
                {
                    NPC.localAI[0]++;
                }
            }
            if (Main.dayTime)
            {
                NPC.localAI[0]++;
            }
            if (NPC.localAI[0] >= 300)
            {
                NPC.active = false;
            }
            #endregion
            #region circle shield and player movement
            int maxDist = 1000;
            for (int i = 0; i < 120; i++)
            {
                double angle = Main.rand.NextDouble() * 2d * Math.PI;
                Vector2 offset = new Vector2((float)Math.Sin(angle) * maxDist, (float)Math.Cos(angle) * maxDist);// unit circle yay
                Dust dust = Main.dust[Dust.NewDust(NPC.Center + offset, 0, 0, 6, 0, 0, 100)];
                dust.noGravity = true;
            }
            for (int i = 0; i < Main.player.Length; i++)
            {
                Player player = Main.player[i];
                if (player.active && !P.dead && Vector2.Distance(player.Center, NPC.Center) > maxDist)
                {
                    Vector2 toTarget = new Vector2(NPC.Center.X - player.Center.X, NPC.Center.Y - player.Center.Y);
                    toTarget.Normalize();
                    float speed = Vector2.Distance(player.Center, NPC.Center) > maxDist + 500 ? 1f : 0.5f;
                    player.velocity += toTarget * 0.5f;

                    player.dashDelay = 2;
                    player.grappling[0] = -1;
                    player.grapCount = 0;
                    for (int p = 0; p < Main.maxProjectiles; p++)
                    {
                        if (Main.projectile[p].active && Main.projectile[p].owner == player.whoAmI && Main.projectile[p].aiStyle == 7)
                        {
                            Main.projectile[p].Kill();
                        }
                    }
                }
            }
            int maxdusts = 5;
            for (int i = 0; i < maxdusts; i++)
            {
                float dustDistance = 100;
                float dustSpeed = 8;
                Vector2 offset = Vector2.UnitX.RotateRandom(MathHelper.Pi) * dustDistance;
                Vector2 velocity = -offset.SafeNormalize(-Vector2.UnitY) * dustSpeed;
                Dust vortex = Dust.NewDustPerfect(new Vector2(NPC.Center.X, NPC.Center.Y) + offset, 6, velocity, 0, default(Color), 1.5f);
                vortex.noGravity = true;
            }
            #endregion
            if (NPC.localAI[1] == 0)
            {
                NPC.Center = P.Center - new Vector2(100, 50);
                NPC.localAI[1]++;
            }
            NPC.ai[0]--;
            if (!NPC.AnyNPCs(ModContent.NPCType<AncientWyrmHead>()))
            {
                if (NPC.ai[0] <= 0)
                {
                    float Speed = 17f;
                    SoundEngine.PlaySound(SoundID.Item20, NPC.position);
                    float rotation = (float)Math.Atan2(NPC.Center.Y - P.Center.Y, NPC.Center.X - P.Center.X);
                    Projectile.NewProjectile(EAU.NPCs(NPC), NPC.Center.X, NPC.Center.Y, (float)((Math.Cos(rotation) * Speed) * -1), (float)((Math.Sin(rotation) * Speed) * -1), ModContent.ProjectileType<GuardianShot>(), projectileBaseDamage + 3, 0f, Main.myPlayer);
                    if (NPC.life > NPC.lifeMax * 0.75f)
                    {
                        NPC.ai[0] = Main.rand.Next(30, 120);
                    }
                    else
                    {
                        NPC.ai[0] = Main.rand.Next(5, 80);
                    }
                }
            }
        }
        public override bool CheckActive()
        {
            return false;
        }
    }
}

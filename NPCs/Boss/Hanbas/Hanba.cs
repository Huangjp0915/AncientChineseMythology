using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    [AutoloadBossHead]
    internal class Hanba : ModNPC
    {
        private int frame;
        private const int maxFrame = 4;
        public static int ReelBackTime => Main.masterMode ? 50 : 60;
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = maxFrame;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 400000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            float angend = MathHelper.Lerp(0, MathHelper.TwoPi, NPC.localAI[0]) + Main.rand.NextFloat(-0.1f, 0.1f);

            //更自然的出生偏移角度（非对称 + 扰动）
            Vector2 spawnOffset = Vector2.UnitY.RotatedBy(angend) * 300f;
            Vector2 destination = target.Center + spawnOffset;
           
            ref float generalTimer = ref NPC.ai[2];
            ref float attackTimer = ref NPC.ai[1];
            ref float state = ref NPC.ai[0];

            Lighting.AddLight(NPC.Center, Color.Red.ToVector3() * NPC.scale);

            float hoverSpeed = 32f;

            NPC.damage = state == 2f ? NPC.defDamage : 0;

            switch (state) {
                case 0f: //靠近预热
                    NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(destination) * hoverSpeed, 0.1f);

                    if (NPC.WithinRange(destination, NPC.velocity.Length() * 1.65f)) {
                        NPC.velocity = NPC.SafeDirectionTo(target.Center) * -7f;
                        state = 1f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1f: //蓄力准备
                    NPC.velocity *= 0.975f;
                    attackTimer++;

                    if (attackTimer >= ReelBackTime) {
                        //冲刺方向扰动
                        float dashAngleOffset = Main.rand.NextFloat(-0.12f, 0.12f);
                        Vector2 dashDir = NPC.SafeDirectionTo(target.Center).RotatedBy(dashAngleOffset);
                        NPC.velocity = dashDir * hoverSpeed;

                        NPC.oldPos = new Vector2[NPC.oldPos.Length];
                        state = 2f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;

                case 2f: //冲刺阶段
                    NPC.knockBackResist = 0f;
                    NPC.damage = 95;
                    if (attackTimer == 0) {
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center, (ActiveSound soundInstance) => {
                            soundInstance.Position = NPC.Center;
                            return true;
                        });
                    }
                    attackTimer++;

                    //冲刺失败后进入短暂思考状态
                    if (attackTimer > 60f || NPC.collideX || NPC.collideY) {
                        NPC.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * 3f;
                        state = 3f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;

                case 3f: //停顿等待阶段（失败后思考）
                    NPC.velocity *= 0.9f;
                    attackTimer++;

                    if (attackTimer > 20f) {
                        if (!VaultUtils.isClient) {
                            NPC.localAI[0] = Main.rand.NextFloat();
                            NPC.netUpdate = true;
                        }
                        
                        state = 0f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;
            }

            generalTimer++;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.02f, 0.1f);

            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

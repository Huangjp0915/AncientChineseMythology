using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 残魂替身头 (V2) — 不再是「本体无敌门」。它是一条**残破灰蓝**的脱体雷龙头, 只有 **唯一招式: 对侧俯冲**:
    /// 移动到玩家相对宿主的另一侧 → 蓄力(灰蓝凝视) → 横贯俯冲。完全可被击杀; 被击破后宿主触发逆雷 + 破绽窗口。
    /// 受伤无减免 (其 realLife 非 ArchosaurHead → ArchosaurBoss.ModifyIncomingHit 不减伤)。
    /// ai[0] = 子状态(0=换位 1=俯冲); ai[3] = 宿主 whoAmI。
    /// </summary>
    public class CloneBossHead : ArchosaurBoss
    {
        private static readonly SoundStyle DiveSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurSummon") { Volume = 0.6f, PitchVariance = .2f, MaxInstances = 3 };

        public override WormType NPCWormType => WormType.Head;
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur_Head";

        private const float ReposTime = 110f, DiveTime = 46f, TelegraphTime = 38f;
        private Vector2 diveDir = Vector2.UnitX;

        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody2>();

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.lifeMax = 130000;   // 可被快速击破 (破绽钥匙, 非血墙)
            NPC.defense = 60;
            NPC.damage = 220;
            SummonMax = 12;         // 短残躯, 区别于宿主长身
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.3f }, NPC.Center);
        }

        public override void AI() {
            base.AI();
            NPC.dontTakeDamage = false; // 替身始终可被伤害

            NPC host = null;
            int hi = (int)NPC.ai[3];
            if (hi >= 0 && hi < Main.maxNPCs && Main.npc[hi].active && Main.npc[hi].ModNPC is ArchosaurHead)
                host = Main.npc[hi];

            Player target = Target;
            Vector2 hostCenter = host?.Center ?? (target.Center - Vector2.UnitY * 400f);
            bool server = Main.netMode != NetmodeID.MultiplayerClient;

            NPC.localAI[0]++;

            if (NPC.ai[0] == 0f) {
                // 换位: 移动到玩家相对宿主的另一侧
                Vector2 dir = (target.Center - hostCenter).SafeNormalize(Vector2.UnitX);
                Vector2 goal = target.Center + dir * 540f;
                Vector2 toGoal = goal - NPC.Center;
                NPC.velocity = (NPC.velocity * 39f + toGoal / 10f) / 40f;

                NPC.localAI[1] = MathHelper.Clamp((NPC.localAI[0] - (ReposTime - TelegraphTime)) / TelegraphTime, 0f, 1f);
                if (!Main.dedServ && NPC.localAI[1] > 0f && Main.rand.NextBool(2)) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Electric, (NPC.Center - from) * 0.07f, 120, default, 1.1f);
                    d.noGravity = true;
                }

                if (NPC.localAI[0] >= ReposTime) {
                    diveDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = diveDir * 27f;
                    NPC.ai[0] = 1f;
                    NPC.localAI[0] = 0f;
                    NPC.localAI[1] = 0f;
                    SoundEngine.PlaySound(DiveSfx, NPC.Center);
                    ACMUtils.AddScreenShake(4f);
                    if (server)
                        NPC.netUpdate = true;
                }
            }
            else {
                // 俯冲
                NPC.velocity *= 1.004f;
                if (!Main.dedServ) {
                    Dust d = Dust.NewDustDirect(NPC.Center - new Vector2(8), 16, 16, DustID.Electric, 0f, 0f, 80, default, 1.3f);
                    d.noGravity = true;
                }
                if (NPC.localAI[0] >= DiveTime) {
                    NPC.ai[0] = 0f;
                    NPC.localAI[0] = 0f;
                    if (server)
                        NPC.netUpdate = true;
                }
            }

            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }

        public override void OnKill() {
            // V2: 不再扣宿主血量; 宿主在 AI 中检测替身消失 → 触发逆雷 + 破绽窗口。
            SoundEngine.PlaySound(SoundID.NPCDeath56 with { Volume = 0.8f }, NPC.Center);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 26; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 60, default, 1.6f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Main.dedServ) {
                Texture2D g = ACMAsset.SoftGlow;
                if (g != null) {
                    // 残破灰蓝 + 蓄力时凝视紫白, 与宿主区分 (这是「弱点钥匙」)
                    float tele = NPC.localAI[1];
                    Color c = Color.Lerp(new Color(110, 140, 190), TelegraphColors.NetherViolet, tele) with { A = 0 };
                    float scale = (1.0f + 0.5f * tele) * (0.6f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f));
                    spriteBatch.Draw(g, NPC.Center - screenPos, null, c * (0.4f + 0.6f * tele), 0f, g.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                }
            }
            // 替身整体染冷灰蓝, 强化「残破」辨识
            Color tint = Color.Lerp(drawColor, new Color(150, 180, 220), 0.4f);
            return base.PreDraw(spriteBatch, screenPos, tint);
        }
    }

    public class CloneBossBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody2>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
            NPC.height = 50;
        }
    }
    public class CloneBossBody2 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<CloneBossBody2>();
            if (SummonCount == SummonMax / 3 * 2 || SummonCount == 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody1>();
            if (SummonCount > SummonMax - 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody3>();
        }
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
        }
    }
    public class CloneBossBody3 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody4>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class CloneBossBody4 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossTail>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class CloneBossTail : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Tail;
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
}

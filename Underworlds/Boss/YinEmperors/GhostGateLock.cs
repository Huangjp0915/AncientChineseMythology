using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 鬼门关钥 —— 镇魂狱（Act II）封印的弱点。
    /// 阴天子施放镇魂封印时，冥眼环绕收缩困住玩家；唯一的逃脱方式是击破挂在封印环上的“鬼门关钥”。
    /// 击破后阴天子的封印瓦解（SealState 由本体在 AI 中检测并切换），开启输出窗口。
    /// 复用冥眼贴图（ArenaEdge.png，存在），以青色高亮区分于普通冥眼。
    /// </summary>
    public class GhostGateLock : ModNPC
    {
        public override string Texture => YinEmperorHelper.Path + "ArenaEdge";

        private const int MaxFrames = 4;

        // ai[0],ai[1] = 封印中心（固定，出生时由本体写入）
        // ai[2] = 计时
        // ai[3] = 环上角度
        private ref float Timer => ref NPC.ai[2];
        private ref float OrbitAngle => ref NPC.ai[3];

        private int frameCounter;
        private int currentFrame;
        private float pulse;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = MaxFrames;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 56;
            NPC.height = 56;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 120000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath6;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.dontCountMe = true;
            NPC.HitSound = SoundID.NPCHit4;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * balance);
        }

        // 不计入 Boss 血条、不进图鉴
        public override bool CheckActive() => false;

        private NPC FindEmperor() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<YinEmperor>() && npc.active)
                    return npc;
            }
            return null;
        }

        public override void AI() {
            NPC owner = FindEmperor();
            if (owner == null || owner.ModNPC is not YinEmperor emperor) {
                NPC.life = 0;
                NPC.active = false;
                return;
            }

            Timer++;
            pulse += 0.15f;

            // 封印已被本体判定为解除/超时 -> 自然消散
            if (emperor.SealState != 0) {
                NPC.life = 0;
                NPC.active = false;
                return;
            }

            // 帧动画
            frameCounter++;
            if (frameCounter >= 6) {
                frameCounter = 0;
                currentFrame = (currentFrame + 1) % MaxFrames;
            }

            Vector2 center = new Vector2(NPC.ai[0], NPC.ai[1]);
            float progress = MathHelper.Clamp(Timer / YinEmperor.SealContractTime, 0f, 1f);
            float radius = MathHelper.Lerp(520f, 110f, ACMUtils.SineInOut(progress));

            OrbitAngle += 0.012f;
            Vector2 desired = center + OrbitAngle.ToRotationVector2() * radius;
            NPC.Center = Vector2.Lerp(NPC.Center, desired, 0.3f);
            NPC.rotation = (center - NPC.Center).ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(NPC.Center, YinEmperorHelper.SoulLanternCyan.ToVector3() * 0.8f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20, 20), DustID.IceTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(NPC.Center, DustID.IceTorch);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }

        public override void OnKill() {
            if (Main.dedServ) return;
            for (int i = 0; i < 40; i++) {
                var d = Dust.NewDustPerfect(NPC.Center, Main.rand.NextBool() ? DustID.IceTorch : DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 2f;
                d.velocity = Main.rand.NextVector2CircularEdge(9, 9);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            int frameHeight = tex.Height / MaxFrames;
            Rectangle src = new Rectangle(0, currentFrame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);
            float p = 1f + MathF.Sin(pulse) * 0.12f;

            // 青色高亮外环（标记“这是弱点”）
            Color glow = YinEmperorHelper.SoulLanternCyan;
            glow.A = 0;
            for (int i = 3; i >= 0; i--) {
                float gs = NPC.scale * (1.3f + i * 0.18f) * p;
                spriteBatch.Draw(tex, NPC.Center - screenPos, src, glow * (0.28f / (i + 1)),
                    NPC.rotation, origin, gs, SpriteEffects.None, 0);
            }

            Color main = Color.Lerp(drawColor, YinEmperorHelper.SoulLanternCyan, 0.5f);
            spriteBatch.Draw(tex, NPC.Center - screenPos, src, main, NPC.rotation, origin, NPC.scale * p, SpriteEffects.None, 0);
            return false;
        }
    }
}

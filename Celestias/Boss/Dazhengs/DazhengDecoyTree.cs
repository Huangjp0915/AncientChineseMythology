using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「黄金幻影·诱饵树」— 秋季「黄金幻象」签名的 DPS 谜题载体 (取代浅薄的幻象弹幕)。
    ///
    /// 触发瞬间在大椿镜像位生成一棵金色虚影树; 此后约 8s 内<b>只有诱饵能被打掉</b>,
    /// 真身大椿无敌并暴露其金色核心(可读提示)。在限时内打爆诱饵 → 真身开启大破绽窗口(防御 −50)。
    /// 这把"换皮幻象"变成"找出真身/抢 DPS"的主动谜题。
    ///
    /// ai[0]=大椿 whoAmI。复用大椿贴图绘制 (金色半透虚影)。逻辑服务器权威, 绘制 client-only。
    /// </summary>
    public class DazhengDecoyTree : ModNPC
    {
        public override string Texture => "AncientChineseMythology/Celestias/Boss/Dazhengs/Dazheng";

        public const int Lifetime = 480; // ~8s

        private int BossIndex => (int)NPC.ai[0];
        private float glowPhase;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 220;
            NPC.height = 260;
            NPC.damage = 0;
            NPC.defense = 30;
            NPC.lifeMax = 320000;
            NPC.HitSound = SoundID.NPCHit7 with { Pitch = 0.3f };
            NPC.DeathSound = SoundID.NPCDeath56;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 0f;
            NPC.aiStyle = -1;
            NPC.dontCountMe = true;

            if (Main.expertMode) NPC.lifeMax = (int)(NPC.lifeMax * 1.4f);
            if (Main.masterMode) NPC.lifeMax = (int)(NPC.lifeMax * 1.6f);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            if (numPlayers > 1)
                NPC.lifeMax = (int)(NPC.lifeMax * (1f + (numPlayers - 1) * 0.45f));
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
        public override bool CheckActive() => false;

        public override void AI() {
            glowPhase += 0.12f;
            NPC.velocity = Vector2.Zero;

            if (BossIndex < 0 || BossIndex >= Main.maxNPCs ||
                !Main.npc[BossIndex].active || Main.npc[BossIndex].type != ModContent.NPCType<Dazheng>()) {
                NPC.active = false;
                return;
            }

            NPC.ai[1]++;
            if (NPC.ai[1] >= Lifetime) {
                // 超时未被打掉: 静默消散, 不给破绽奖励
                NPC.active = false;
                if (Main.netMode != NetmodeID.Server)
                    SpawnDissipate(false);
                return;
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 p = NPC.Center + Main.rand.NextVector2Circular(120, 150);
                Dust d = Dust.NewDustPerfect(p, DustID.GoldFlame, Vector2.Zero, 100, default, 1.6f);
                d.noGravity = true;
                d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * 1.5f;
            }

            // 伤害叙事: 血量越低, 金箔剥落越急 (镀金正在被打穿的持续读法)
            float hurt = 1f - MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
            if (Main.netMode != NetmodeID.Server && hurt > 0.15f && Main.rand.NextFloat() < hurt * 0.5f) {
                Vector2 p = NPC.Center + Main.rand.NextVector2Circular(110, 140);
                Dust flake = Dust.NewDustPerfect(p, DustID.GoldCoin,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f)), 60, default, 0.9f + hurt * 0.6f);
                flake.noGravity = false;
            }

            float dim = 1f - hurt * 0.45f;
            Lighting.AddLight(NPC.Center, 0.6f * dim, 0.5f * dim, 0.15f * dim);
        }

        public override void OnKill() {
            // 打爆诱饵: 通知大椿开启破绽窗口
            Dazheng.DecoyKilled = true;
            Dazheng.DecoyEventFrame = Main.GameUpdateCount;

            if (Main.netMode != NetmodeID.Server)
                SpawnDissipate(true);
        }

        private void SpawnDissipate(bool killed) {
            int count = killed ? 40 : 18;
            for (int i = 0; i < count; i++) {
                float a = MathHelper.TwoPi / count * i;
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GoldFlame,
                    new Vector2(MathF.Cos(a), MathF.Sin(a)) * Main.rand.NextFloat(3f, killed ? 10f : 5f),
                    80, default, killed ? 2.2f : 1.4f);
                d.noGravity = true;
            }
            if (killed) {
                SoundEngine_PlayGold(NPC.Center);
                ACMUtils.AddScreenShake(6f);
            }
        }

        private static void SoundEngine_PlayGold(Vector2 pos) {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f }, pos);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Rectangle frame = new(0, 0, tex.Width, tex.Height);
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            float fadeIn = MathHelper.Clamp(NPC.ai[1] / 24f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Lifetime - NPC.ai[1]) / 40f, 0f, 1f);
            float alpha = fadeIn * fadeOut;
            float p = 1f + MathF.Sin(glowPhase) * 0.04f;
            // 镜像翻转, 强化"虚影/镜像"的辨识感
            SpriteEffects fx = SpriteEffects.FlipHorizontally;

            // 伤害叙事: 金色随血量变哑 (亮金 → 哑铜), 快破时高频颤抖
            float hurt = 1f - MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
            Vector2 tremor = hurt > 0.6f
                ? new Vector2(MathF.Sin(glowPhase * 17f), MathF.Cos(glowPhase * 13f)) * (hurt - 0.6f) * 8f
                : Vector2.Zero;
            drawPos += tremor;

            // 金色幽影底光
            Color outer = (Color.Lerp(new Color(255, 200, 60), new Color(150, 100, 45), hurt) with { A = 0 }) * (0.5f * alpha);
            spriteBatch.Draw(tex, drawPos, frame, outer, 0f, origin, NPC.scale * 1.04f * p, fx, 0f);
            Color body = (Color.Lerp(new Color(255, 225, 120), new Color(190, 140, 70), hurt) with { A = 0 }) * (0.75f * alpha);
            spriteBatch.Draw(tex, drawPos, frame, body, 0f, origin, NPC.scale, fx, 0f);

            return false;
        }
    }
}

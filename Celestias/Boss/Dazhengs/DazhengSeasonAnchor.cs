using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「季节锚点」— 战场四角的季节图腾, 是 G5 门控与季节控制的核心交互体。
    ///
    /// ● <b>G5 入场门控:</b> 入场后大椿先无敌(暴露根脉), 四角生成四季锚点; 玩家须先<b>毁掉 3 个</b>
    ///   才让大椿进入可受伤的季节循环——让 4.5M 血"挣得起"(机制检验而非纯数值)。
    /// ● <b>季节控制:</b> 门控后锚点缓慢复生; 击毁任一锚点 → 立即切换到该锚点的主导季节 +
    ///   开启大椿短暂破绽窗口(防御 −50)。即"主动用季节解谜控制战斗"。
    ///
    /// ai[0]=大椿 whoAmI; ai[1]=季节(0~3, 见 <see cref="DazhengSeasons"/>)。
    /// 纯目标体: 不伤害玩家、不计 Boss 死亡、Boss 消失即清场。逻辑服务器权威, 绘制 client-only。
    /// </summary>
    public class DazhengSeasonAnchor : ModNPC
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private int BossIndex => (int)NPC.ai[0];
        public int Season => (int)NPC.ai[1];

        private float pulse;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            NPC.width = 90;
            NPC.height = 90;
            NPC.damage = 0;
            NPC.defense = 40;
            NPC.lifeMax = 130000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath6;
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
            // 锚点是 DPS 检验, 随队伍规模温和增血
            if (numPlayers > 1)
                NPC.lifeMax = (int)(NPC.lifeMax * (1f + (numPlayers - 1) * 0.4f));
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
        public override bool CheckActive() => false;
        public override bool? CanFallThroughPlatforms() => true;

        public override void AI() {
            pulse += 0.08f;

            // Boss 不在场 → 清场
            if (BossIndex < 0 || BossIndex >= Main.maxNPCs ||
                !Main.npc[BossIndex].active || Main.npc[BossIndex].type != ModContent.NPCType<Dazheng>()) {
                NPC.life = 0;
                NPC.active = false;
                return;
            }

            NPC.velocity = Vector2.Zero;

            // 季节色尘埃环绕, 明示其季节身份
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Color sc = DazhengSeasons.Accent(Season);
                float a = pulse + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 p = NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 70f;
                Dust d = Dust.NewDustPerfect(p, DustID.GoldFlame, Vector2.Zero, 120, sc, 1.4f);
                d.noGravity = true;
                d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * 2.2f;
            }

            Lighting.AddLight(NPC.Center, DazhengSeasons.Tint(Season).ToVector3() * 0.6f);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.netMode == NetmodeID.Server)
                return;
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GrassBlades,
                    hit.HitDirection * 2f, -1f, 120, DazhengSeasons.Accent(Season), 1.3f);
                d.noGravity = true;
            }
        }

        public override void OnKill() {
            // 通知大椿: 季节切换 + 破绽窗口 (服务器权威, 大椿在 AI 中消费)
            Dazheng.RecentBrokenSeason = Season;
            Dazheng.RecentBrokenFrame = Main.GameUpdateCount;

            if (Main.netMode == NetmodeID.Server)
                return;

            Color sc = DazhengSeasons.Tint(Season);
            for (int i = 0; i < 28; i++) {
                float a = MathHelper.TwoPi / 28 * i;
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GoldFlame,
                    new Vector2(MathF.Cos(a), MathF.Sin(a)) * Main.rand.NextFloat(3f, 8f), 80, sc, 1.8f);
                d.noGravity = true;
            }
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GrassBlades,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.5f);
                d.noGravity = false;
            }
            ACMUtils.AddScreenShake(4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            Texture2D glow = ACMAsset.SoftGlow;
            Texture2D star = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;
            Color tint = DazhengSeasons.Tint(Season);
            Color accent = DazhengSeasons.Accent(Season);
            float p = 1f + MathF.Sin(pulse) * 0.12f;

            // 供养根线: 锚点 → 大椿的细能量导管, 把"毁锚点伤树神"的因果画在屏幕上。
            // 门控期 (树神无敌吸食四季之力) 更亮; 战斗期转暗淡维持读法。
            if (BossIndex >= 0 && BossIndex < Main.maxNPCs) {
                NPC boss = Main.npc[BossIndex];
                if (boss.active && boss.ModNPC is Dazheng dz) {
                    float feed = dz.GatePassed ? 0.22f : 0.42f;
                    float fp = 0.8f + 0.2f * MathF.Sin(pulse * 2f + Season);
                    ACMShaders.DrawBeam(NPC.Center, boss.Center, 4.5f,
                        accent, tint * 0.5f, feed * fp, flowSpeed: 1.8f, flowScale: 3f, coreSharp: 2.4f);
                }
            }

            // 血量越低越闪烁(可破提示)
            float lifeFrac = MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
            float urgency = 1f + (1f - lifeFrac) * MathF.Abs(MathF.Sin(pulse * 3f)) * 0.5f;

            SpriteBatch sb = spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                sb.Draw(glow, drawPos, null, (tint with { A = 0 }) * 0.5f, 0f, go, 1.6f * p * urgency, SpriteEffects.None, 0f);
                sb.Draw(glow, drawPos, null, (accent with { A = 0 }) * 0.6f, 0f, go, 0.9f * p, SpriteEffects.None, 0f);
                sb.Draw(glow, drawPos, null, (Color.White with { A = 0 }) * 0.4f, 0f, go, 0.45f * p, SpriteEffects.None, 0f);
            }
            if (star != null) {
                Vector2 so = star.Size() / 2f;
                sb.Draw(star, drawPos, null, (accent with { A = 0 }) * 0.7f, pulse * 0.6f, so, 0.5f * p * urgency, SpriteEffects.None, 0f);
                sb.Draw(star, drawPos, null, (Color.White with { A = 0 }) * 0.5f, -pulse * 0.4f, so, 0.3f * p, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 汲魂灵芽 — 树精 P2 治疗反制单位 (打靶式 DPS 权衡, 与大椿"冲刺挣断导管"差异化)。
    ///
    /// 树精每轮循环开头的潜地冒出时在地面种下一株 (全场 ≤1):
    ///  - 存活期间以可见翠绿汲取光束连向树精, 每秒回复树精 0.35% 上限血;
    ///  - 回血封顶: 树精血量不会被抬回 49% 以上 (防 P2 相位回退);
    ///  - 玩家击杀 → 汲取中断播报 + 爆浆 + 树精痛苦摆动 (转火有明确正反馈);
    ///  - 12s 后自行缩回土中 (放着不管 ≈ 吃掉 ~4.2% 血)。
    ///
    /// ai[0] = 树精 whoAmI。血量在服务器首帧按树精上限 2% 校准。
    /// 无接触伤害; 视觉全程序化 (SoftGlow 苞体 + Sparkle 花瓣 + DrawBeam 汲取导管)。
    /// </summary>
    public class DryadsSiphonBud : ModNPC
    {
        // 复用同目录贴图满足资源加载; 本体走程序化绘制
        public override string Texture => "AncientChineseMythology/Celestias/Boss/Dryades/Acanthosphere";

        public const int LifeTime = 720;      // 12s 自行缩回
        private const float HealPerSecond = 0.0035f;  // 0.35%/s
        private const float HealCapFrac = 0.49f;      // 回血封顶 (防相位回退)

        private int BossIndex => (int)NPC.ai[0];
        private ref float Timer => ref NPC.ai[1];

        private float pulsePhase;
        private bool calibrated;

        private static readonly Color BudGreen = new(140, 240, 90);
        private static readonly Color BudCore = new(220, 255, 170);

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 44;
            NPC.height = 56;
            NPC.damage = 0;
            NPC.defense = 20;
            NPC.lifeMax = 20000; // 首帧按树精上限 2% 校准
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = 0.4f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = 0.5f };
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 1f;
            NPC.value = 0;
        }

        public override void AI() {
            pulsePhase += 0.08f;
            Timer++;

            // 宿主失效 → 缩回
            if (BossIndex < 0 || BossIndex >= Main.maxNPCs || !Main.npc[BossIndex].active ||
                Main.npc[BossIndex].type != ModContent.NPCType<Dryads>()) {
                Retract();
                return;
            }

            NPC boss = Main.npc[BossIndex];

            // 服务器首帧: 按树精上限校准芽血量 (2%)
            if (!calibrated) {
                calibrated = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.lifeMax = Math.Max(1000, (int)(boss.lifeMax * 0.02f));
                    NPC.life = NPC.lifeMax;
                    NPC.netUpdate = true;
                }
            }

            // 出土动画期 (前 20f) 缓慢升起
            if (Timer < 20f)
                NPC.position.Y -= 0.8f;

            // —— 汲取回血 (服务器权威, 封顶 49%) ——
            if (Timer >= 20f && Main.netMode != NetmodeID.MultiplayerClient && Timer % 60f == 0f) {
                int heal = (int)(boss.lifeMax * HealPerSecond);
                int cap = (int)(boss.lifeMax * HealCapFrac);
                if (boss.life < cap) {
                    heal = Math.Min(heal, cap - boss.life);
                    if (heal > 0) {
                        boss.life += heal;
                        boss.HealEffect(heal, true);
                    }
                }
            }

            // 汲取粒子: 芽 → 树精 回流光尘
            if (Main.netMode != NetmodeID.Server && Timer >= 20f && Main.rand.NextBool(2)) {
                Vector2 along = Vector2.Lerp(NPC.Center, boss.Center, Main.rand.NextFloat(0.15f, 0.9f));
                Dust d = Dust.NewDustPerfect(along + Main.rand.NextVector2Circular(12, 12),
                    DustID.GreenFairy, Vector2.Zero, 120, default, 1.1f);
                d.noGravity = true;
                d.velocity = (boss.Center - along).SafeNormalize(Vector2.Zero) * 3.5f;
            }

            // 苞体呼吸光
            Lighting.AddLight(NPC.Center, new Vector3(0.18f, 0.4f, 0.1f) * (0.7f + MathF.Sin(pulsePhase) * 0.3f));

            // 寿命尽 → 自然缩回
            if (Timer >= LifeTime)
                Retract();
        }

        /// <summary>自然缩回土中 (非击杀, 无奖励反馈)。</summary>
        private void Retract() {
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture,
                        Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2.5f), 120, default, 1.3f);
                    d.noGravity = false;
                }
            }
            NPC.active = false;
            NPC.life = 0;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI); // 立即广播失效
        }

        /// <summary>击杀反馈走 HitEffect (多人时每个客户端都会调用): 汲取中断播报 + 爆浆。</summary>
        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.netMode == NetmodeID.Server || NPC.life > 0)
                return;
            string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.DryadsSiphonBud.Severed");
            CombatText.NewText(NPC.getRect(), BudGreen, text, true);
            SoundEngine.PlaySound(SoundID.NPCDeath22 with { Pitch = 0.35f, Volume = 0.8f }, NPC.Center);
            for (int i = 0; i < 26; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    Main.rand.NextBool() ? DustID.GreenFairy : DustID.JungleSpore,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, 1f), 60, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1f;
            return null;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            float emerge = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float lifeFrac = NPC.life / (float)NPC.lifeMax;
            Vector2 drawPos = NPC.Center - screenPos;

            // —— 汲取光束 (翠绿导管, 受伤变细) ——
            if (BossIndex >= 0 && BossIndex < Main.maxNPCs && Main.npc[BossIndex].active && Timer >= 20f) {
                NPC boss = Main.npc[BossIndex];
                float flow = 0.5f + MathF.Sin(pulsePhase * 2f) * 0.2f;
                ACMShaders.DrawBeam(NPC.Center, boss.Center, (5f + 5f * lifeFrac) * emerge,
                    BudCore, BudGreen * 0.7f, (0.45f + flow * 0.3f) * emerge,
                    flowSpeed: 1.8f, flowScale: 1.6f);
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // —— 苞体: 双层 SoftGlow 呼吸 + Sparkle 花瓣旋转 ——
            Texture2D glow = ACMAsset.SoftGlow;
            Texture2D petal = ACMAsset.Sparkle;
            float breath = 1f + MathF.Sin(pulsePhase) * 0.12f;
            float hurtDim = 0.45f + lifeFrac * 0.55f;

            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                sb.Draw(glow, drawPos, null, BudGreen with { A = 0 } * (0.85f * emerge * hurtDim),
                    0f, go, 1.5f * breath * emerge, SpriteEffects.None, 0f);
                sb.Draw(glow, drawPos, null, BudCore with { A = 0 } * (0.9f * emerge * hurtDim),
                    0f, go, 0.7f * breath * emerge, SpriteEffects.None, 0f);
            }
            if (petal != null) {
                Vector2 po = petal.Size() / 2f;
                for (int i = 0; i < 3; i++) {
                    float rot = pulsePhase * 0.35f + MathHelper.TwoPi / 3f * i;
                    sb.Draw(petal, drawPos, null, BudGreen with { A = 0 } * (0.55f * emerge * hurtDim),
                        rot, po, 0.62f * breath * emerge, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }
}

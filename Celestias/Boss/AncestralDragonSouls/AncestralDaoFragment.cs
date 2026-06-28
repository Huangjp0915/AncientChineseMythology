using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 刀魂碎片 (Dao Fragment) — 狂暴终曲「刀魂碎片场」谜题节点。
    /// 8 颗环绕祖龙真身的太初碎片, **每颗须被击中一次方可消解**; 全部消解前祖龙 dontTakeDamage 不吃伤
    /// (谜题窗口)。一击即碎 (lifeMax=1), 由玩家击破 → 解锁祖龙受创窗口; 超时未破 → 由 Boss 强制引爆成弹幕。
    /// 太初青白配色, **绝不用红** (它不是致命预警, 而是"先破我"的可读目标)。纯逻辑节点: 服务器权威生成/同步,
    /// 不掉落、不计入刷怪上限、不显示血条。
    /// </summary>
    public class AncestralDaoFragment : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public ref float OwnerIndex => ref NPC.ai[0];
        public ref float BaseAngle => ref NPC.ai[1];
        public ref float OrbitRadius => ref NPC.ai[2];

        private float pulse;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 2;
            NPCID.Sets.TrailCacheLength[Type] = 8;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 42;
            NPC.height = 42;
            NPC.lifeMax = 1;          // 一击即碎 = "命中一次即消解"
            NPC.defense = 0;
            NPC.damage = 130;         // 轻量接触压制
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.lavaImmune = true;
            NPC.dontCountMe = true;   // 不计入刷怪上限
            NPC.npcSlots = 0f;
            NPC.HitSound = SoundID.Item27 with { Pitch = 0.5f };
            NPC.DeathSound = SoundID.Item25 with { Pitch = 0.4f };
            NPC.value = 0f;
        }

        // 不参与自然消失逻辑, 生命周期由 owner Boss 控制
        public override bool CheckActive() => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        public override void AI() {
            pulse += 0.13f;

            int owner = (int)OwnerIndex;
            if (owner < 0 || owner >= Main.maxNPCs) {
                NPC.active = false;
                return;
            }
            NPC owNpc = Main.npc[owner];
            // owner 离场或已脱离谜题阶段 → 自行消失 (由 Boss 决定引爆与否)
            if (!owNpc.active || owNpc.ModNPC is not AncestralDragonSoulHead head || !head.DaoFieldArming) {
                NPC.active = false;
                return;
            }

            // 环绕 owner 缓慢旋转
            float ang = BaseAngle + (float)Main.GlobalTimeWrappedHourly * 0.55f;
            float r = OrbitRadius <= 1f ? 280f : OrbitRadius;
            Vector2 target = owNpc.Center + ang.ToRotationVector2() * r;
            NPC.Center = Vector2.Lerp(NPC.Center, target, 0.18f);
            NPC.velocity = Vector2.Zero;
            NPC.rotation += 0.08f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan,
                    0, 0, 150, Color.White, 1.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f;
            }

            Lighting.AddLight(NPC.Center, new Vector3(0.7f, 0.85f, 1f) * 0.8f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D glow = ACMAsset.LightShot ?? ACMAsset.SoftGlow;
            Texture2D sparkle = ACMAsset.Sparkle;
            if (glow == null)
                return false;

            Vector2 drawPos = NPC.Center - screenPos;
            float p = 1f + MathF.Sin(pulse) * 0.18f;

            // 拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float prog = 1f - (float)i / NPC.oldPos.Length;
                Color tcol = TelegraphColors.Holy * prog * 0.30f;
                tcol.A = 0;
                Vector2 tpos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                spriteBatch.Draw(glow, tpos, null, tcol, 0f, glow.Size() / 2f, 0.55f * prog, SpriteEffects.None, 0f);
            }

            // 外晕 (太初青白)
            Color outer = new Color(190, 225, 255) * 0.6f * p;
            outer.A = 0;
            spriteBatch.Draw(glow, drawPos, null, outer, 0f, glow.Size() / 2f, 1.1f * p, SpriteEffects.None, 0f);

            // 符文核心
            if (sparkle != null) {
                Color rune = TelegraphColors.Holy * 0.9f;
                rune.A = 0;
                spriteBatch.Draw(sparkle, drawPos, null, rune, NPC.rotation, sparkle.Size() / 2f, 0.7f * p, SpriteEffects.None, 0f);
                spriteBatch.Draw(sparkle, drawPos, null, rune * 0.6f, -NPC.rotation * 1.3f, sparkle.Size() / 2f, 0.5f * p, SpriteEffects.None, 0f);
            }

            Color core = Color.White * 0.85f;
            core.A = 0;
            spriteBatch.Draw(glow, drawPos, null, core, 0f, glow.Size() / 2f, 0.5f * p, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill() {
            // 被玩家击破 = 干净消解 (无弹幕惩罚), 给爽快反馈
            if (Main.netMode == NetmodeID.Server)
                return;

            ACMUtils.AddScreenShake(2f);
            for (int i = 0; i < 22; i++) {
                float a = MathHelper.TwoPi * i / 22f;
                Vector2 vel = a.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan,
                    vel.X, vel.Y, 120, Color.White, 1.6f);
                Main.dust[dust].noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);
        }
    }
}

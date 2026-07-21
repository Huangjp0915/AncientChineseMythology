using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙之尾 — 末端体节。绘制移交头部统一处理; 接触伤害下调至头部的 45%。
    /// </summary>
    public class AzureDragonTail : AzureDragon
    {
        public override WormType NPCWormType => WormType.Tail;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 50;
            NPC.height = 50;
            NPC.damage = (int)(NPC.damage * 0.45f);
        }

        public override void AI() {
            base.AI();

            // 尾尖高速时的电弧尾迹 (速度门控)
            if (!VaultUtils.isServer && NPC.velocity.LengthSquared() > 144f && Main.rand.NextBool(3)) {
                Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 26f
                    + Main.rand.NextVector2Circular(18f, 18f);
                int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 90, default, 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -NPC.velocity * 0.15f;
            }
        }

        // 绘制由头部统一承担
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
    }
}

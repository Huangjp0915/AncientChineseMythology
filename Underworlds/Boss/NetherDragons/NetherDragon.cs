using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙基础类
    /// </summary>
    public abstract class NetherDragon : BasicWorm
    {
        public override bool IsUseSpriteDirection => true;

        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.lifeMax = 80000;
            NPC.damage = 80;
            NPC.defense = 35;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            SummonMax = 60;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation + MathHelper.PiOver2 * NPC.spriteDirection;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (NPCWormType != WormType.Head) {
                return false;
            }
            return null;
        }

        public override void AI() {
            base.AI();
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            if (FatherNPC.Alives()) {
                Vector2 pos = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;
                for (int i = 0; i < NPC.velocity.Length() / 2; i++) {
                    int dust = Dust.NewDust(pos, 1, 1, DustID.BlueTorch);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.6f);
                }

                // 身体段在雾气中移动时产生少量粒子（降低频率）
                if (Main.netMode != NetmodeID.Server && NetherDragonFogSystem.IsActive) {
                    float fogDensity = NetherDragonFogSystem.GetFogDensityAt(NPC.Center);
                    if (fogDensity > 0.7f && Main.rand.NextBool(8)) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(30f, 30f);
                        int fogDust = Dust.NewDust(dustPos, 1, 1, DustID.BlueTorch, 0, 0, 100, Color.Cyan, 0.6f);
                        Main.dust[fogDust].noGravity = true;
                        Main.dust[fogDust].velocity = Main.rand.NextVector2Circular(1f, 1f);
                        Main.dust[fogDust].alpha = 200;
                    }
                }
            }

            // 发光效果
            Lighting.AddLight(NPC.Center, 0.1f, 0.3f, 0.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            if (NPCWormType == WormType.Head) {
                origin.Y = tex.Height / 2;
            }

            // 蓝色幽冥色调
            Color netherColor = Color.Lerp(drawColor, new Color(100, 150, 255), 0.4f);

            // 根据雾气密度轻微调整颜色
            if (Main.netMode != NetmodeID.Server && NetherDragonFogSystem.IsActive) {
                float fogDensity = NetherDragonFogSystem.GetFogDensityAt(NPC.Center);
                if (fogDensity > 0.7f) {
                    netherColor = Color.Lerp(netherColor, new Color(80, 120, 200), fogDensity * 0.15f);
                }
            }

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2,
                origin, NPC.scale, NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);

            return false;
        }
    }
}

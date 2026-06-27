using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙鳞·怨念账珠 (Grudge Scale Orb) —— P3《噬墓》签名机制的可破坏目标。
    ///
    /// 幽冥龙在 P3 蜕下 2~3 片龙鳞, 绕玩家缓慢公转 (可读、非弹幕)。玩家须在限时窗口内**击毁**它们:
    ///   ● 全部击毁 → 阻止龙的 <b>暴怒吐息</b> (清账反制)。
    ///   ● 有残留 → 龙读取怨念账触发暴怒 (暴怒 = 移动更快, **不是**喷火更密 — 反模式禁区)。
    ///
    /// 是可被玩家武器击杀的低血 ModNPC; 触碰叠 <see cref="UnderworldField"/> 魂蚀。
    /// 头部通过遍历 <see cref="NPC.ai"/>[0]==头索引 统计存活数, 服务器权威。
    /// </summary>
    internal class NetherScaleOrb : ModNPC
    {
        // 复用幽冥龙鳞素材贴图 (on-disk, 自动加载安全)
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Materials/NetherDragonScale";

        public ref float HeadIndex => ref NPC.ai[0];
        public ref float OrbitAngle => ref NPC.ai[1];
        public ref float TargetPlayer => ref NPC.ai[2];

        private float pulse;

        public override void SetStaticDefaults() {
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 36;
            NPC.height = 36;
            NPC.lifeMax = 900;
            NPC.damage = 45;
            NPC.defense = 10;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit36;
            NPC.DeathSound = SoundID.NPCDeath39;
            NPC.lifeMax = Main.masterMode ? 1500 : (Main.expertMode ? 1100 : 900);
            NPC.dontCountMe = true;
        }

        public override void AI() {
            pulse += 0.12f;

            // 目标玩家
            int tp = (int)TargetPlayer;
            if (tp < 0 || tp >= Main.maxPlayers || !Main.player[tp].active || Main.player[tp].dead) {
                NPC.TargetClosest(false);
                tp = NPC.target;
                TargetPlayer = tp;
            }
            Player target = Main.player[tp];
            if (!target.active || target.dead) {
                NPC.life = 0;
                NPC.checkDead();
                NPC.active = false;
                return;
            }

            // 缓慢公转 (可读, 非弹幕)
            OrbitAngle += 0.035f;
            float radius = 230f + MathF.Sin(pulse * 0.3f) * 22f;
            Vector2 desired = target.Center + OrbitAngle.ToRotationVector2() * radius;
            NPC.Center = Vector2.Lerp(NPC.Center, desired, 0.10f);
            NPC.velocity = Vector2.Zero;
            NPC.rotation += 0.05f;

            Lighting.AddLight(NPC.Center, 0.25f, 0.45f, 0.3f);

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 120,
                    new Color(110, 230, 150), 1.1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.3f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            int n = NPC.life <= 0 ? 22 : 5;
            for (int i = 0; i < n; i++) {
                int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 100,
                    new Color(110, 230, 150), 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = NPC.Center - screenPos;
            float glow = 1f + MathF.Sin(pulse) * 0.18f;
            Color tint = new Color(160, 255, 200);

            // 鬼绿外晕 + 本体 (玩家需击毁的可读目标)
            for (int i = 0; i < 4; i++) {
                Vector2 off = (i * MathHelper.PiOver2 + pulse * 0.4f).ToRotationVector2() * 3f;
                spriteBatch.Draw(tex, pos + off, null, tint * 0.30f, NPC.rotation, origin, NPC.scale * glow * 1.25f,
                    SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(tex, pos, null, Color.Lerp(drawColor, tint, 0.5f), NPC.rotation, origin, NPC.scale,
                SpriteEffects.None, 0f);
            return false;
        }

        public override bool CheckActive() => true;
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥逆鳞 (Reverse Scale) —— P3《噬墓》受击蜕落的清账反制目标。
    ///
    /// V3: 从"定时蜕鳞"改为<b>受击掉落的逆鳞反击</b> — 玩家打得越狠, 蜕落越快 (头部按受击
    /// 伤害累计蜕落, 场上至多 2 枚)。逆鳞绕玩家公转, 携带 <see cref="FuseTime"/> 引信:
    ///   ● 限时内击毁 → 掉落生命之心 + 削减幽冥龙怨念账 (清账反制, 削弱万魂门规模)。
    ///   ● 超时未清 → 碎裂并触发头部<b>暴怒</b> (提速+火幕增密; 前摇不变 — 可读性底线)。
    /// 引信以 12 点倒计时环可视化 (幽紫 → 处决赤红), 无需 UI。
    ///
    /// ai[0]=头索引, ai[1]=公转起始角, ai[2]=目标玩家, ai[3]=引信计时 (确定性推进)。
    /// </summary>
    internal class NetherScaleOrb : ModNPC
    {
        // 复用幽冥龙鳞素材贴图 (on-disk, 自动加载安全)
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Materials/NetherDragonScale";

        public ref float HeadIndex => ref NPC.ai[0];
        public ref float OrbitAngle => ref NPC.ai[1];
        public ref float TargetPlayer => ref NPC.ai[2];
        public ref float FuseTimer => ref NPC.ai[3];

        public const int FuseTime = 480;

        private float pulse;
        private float spawnFly;   // 出膛弧线插值 0~1

        public override void SetStaticDefaults() {
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 36;
            NPC.height = 36;
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
            FuseTimer++;

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

            // 出膛 40f: 弧线飞向轨道 (期间无接触伤害, 见 CanHitPlayer)
            spawnFly = MathHelper.Clamp(FuseTimer / 40f, 0f, 1f);

            // 公转 (可读、非弹幕)
            OrbitAngle += 0.030f;
            float radius = 260f + MathF.Sin(pulse * 0.3f) * 20f;
            Vector2 desired = target.Center + OrbitAngle.ToRotationVector2() * radius;
            NPC.Center = Vector2.Lerp(NPC.Center, desired, 0.04f + spawnFly * 0.06f);
            NPC.velocity = Vector2.Zero;
            NPC.rotation += 0.05f;

            // 超时: 碎裂 → 头部暴怒 (服务器权威)
            if (FuseTimer >= FuseTime) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int hi = (int)HeadIndex;
                    if (hi >= 0 && hi < Main.maxNPCs && Main.npc[hi].active &&
                        Main.npc[hi].ModNPC is NetherDragonHead head)
                        head.TriggerEnrage();
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
                return;
            }

            Lighting.AddLight(NPC.Center, 0.25f, 0.45f, 0.3f);

            // 引信将尽: 粒子渐急 (音画双通道预警)
            float urgency = FuseTimer / (float)FuseTime;
            if (!Main.dedServ && Main.rand.NextBool(urgency > 0.7f ? 2 : 4)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(18f, 18f),
                    urgency > 0.7f ? DustID.RedTorch : DustID.GreenTorch, Vector2.Zero, 120,
                    urgency > 0.7f ? TelegraphColors.Execution : new Color(110, 230, 150), 1.1f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(0.8f, 0.8f) + new Vector2(0, -0.8f);
            }
            if ((int)FuseTimer % 60 == 0 && urgency > 0.55f)
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.3f + urgency * 0.7f }, NPC.Center);
        }

        // 出膛弧线期不造成接触伤害 (防"刚蜕出来就撞脸")
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => FuseTimer >= 40f;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override void OnKill() {
            // 玩家清账成功: 掉心 + 削减怨念账 (万魂门规模随之缩水)
            if (Main.netMode != NetmodeID.MultiplayerClient && FuseTimer < FuseTime) {
                int hi = (int)HeadIndex;
                if (hi >= 0 && hi < Main.maxNPCs && Main.npc[hi].active &&
                    Main.npc[hi].ModNPC is NetherDragonHead) {
                    NPC head = Main.npc[hi];
                    UnderworldField.ReduceGrudge(head, (int)(head.lifeMax * 0.07f));
                }
                Item.NewItem(NPC.GetSource_Death(), NPC.getRect(), ItemID.Heart);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            int n = NPC.life <= 0 ? 24 : 5;
            for (int i = 0; i < n; i++) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.GreenTorch, Vector2.Zero, 100, new Color(110, 230, 150), 1.6f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4.5f, 4.5f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = NPC.Center - screenPos;
            float glow = 1f + MathF.Sin(pulse) * 0.18f;
            float urgency = MathHelper.Clamp(FuseTimer / (float)FuseTime, 0f, 1f);
            Color tint = Color.Lerp(new Color(160, 255, 200), TelegraphColors.Execution,
                MathHelper.Clamp((urgency - 0.55f) / 0.45f, 0f, 1f) * 0.7f);

            // 引信倒计时环: 12 点位, 亮点数 = 剩余时间 (无需 UI 的可读引信)
            Texture2D soft = ACMAsset.SoftGlow;
            if (soft != null) {
                int lit = (int)MathF.Ceiling((1f - urgency) * 12f);
                for (int i = 0; i < 12; i++) {
                    float a = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 12f;
                    Vector2 p = pos + a.ToRotationVector2() * 34f;
                    bool on = i < lit;
                    Color c = on ? tint with { A = 0 } : new Color(40, 40, 60, 0);
                    spriteBatch.Draw(soft, p, null, c * (on ? 0.8f : 0.25f), 0f,
                        soft.Size() / 2f, on ? 0.16f : 0.10f, SpriteEffects.None, 0f);
                }
            }

            // 鬼绿外晕 + 本体 (需击毁的可读目标)
            for (int i = 0; i < 4; i++) {
                Vector2 off = (i * MathHelper.PiOver2 + pulse * 0.4f).ToRotationVector2() * 3f;
                spriteBatch.Draw(tex, pos + off, null, (tint with { A = 0 }) * 0.30f, NPC.rotation, origin,
                    NPC.scale * glow * 1.25f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(tex, pos, null, Color.Lerp(drawColor, tint, 0.5f), NPC.rotation, origin, NPC.scale,
                SpriteEffects.None, 0f);
            return false;
        }

        public override bool CheckActive() => true;
    }
}

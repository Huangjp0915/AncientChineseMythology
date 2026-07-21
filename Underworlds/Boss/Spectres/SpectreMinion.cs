using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 提灯冤魂 — 怨灵《附身》幕召唤的门控分身 (V3)。
    /// 入场溶解凝形 (SpectreVeil)；突袭遵循 launch/brake 语法：预告线 36f → 单帧 17→22 冲刺 → 硬刹。
    /// 冲刺前摇期间减速锁向 (公平阀门)；射击带 260px 最小射距 (贴身可压制它)。
    /// 死亡即"清账"：怨念 -3 + 魂缕飞向最近灯笼 (玩家反制的可视化正反馈)。
    /// </summary>
    public class SpectreMinion : ModNPC
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private float pulsePhase;
        private float orbitAngle;
        private int attackTimer;
        private float spawnDissolve = 1f; // 凝形进度 (1=未成形)

        // 俯冲突袭 (telegraphed lunge)
        private int lungeState; // 0=游荡 1=预告 2=俯冲 3=刹车
        private int lungeTimer;
        private Vector2 lungeDir = Vector2.UnitX;
        private const int LungeWindup = 36;
        private const int LungeInterval = 190;
        private const float MinFireDist = 260f;

        private int OwnerIndex => (int)NPC.ai[0];

        private NPC Owner {
            get {
                if (OwnerIndex >= 0 && OwnerIndex < Main.maxNPCs && Main.npc[OwnerIndex].active)
                    return Main.npc[OwnerIndex];
                return null;
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 40;
            NPC.damage = 50;
            NPC.defense = 20;
            NPC.lifeMax = 8000;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = 0f;
            NPC.knockBackResist = 0.2f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.alpha = 0;
        }

        public override void AI() {
            // 宿主死亡则自身消散
            if (Owner == null || !Owner.active) {
                NPC.life = 0;
                NPC.active = false;
                OnKill();
                return;
            }

            pulsePhase += 0.1f;
            orbitAngle += 0.03f;
            attackTimer++;

            // 凝形入场 (前 36f 无敌无伤, 溶解成形)
            spawnDissolve = MathHelper.Clamp(spawnDissolve - 1f / 36f, 0f, 1f);
            if (spawnDissolve > 0.05f) {
                NPC.dontTakeDamage = true;
                NPC.velocity *= 0.9f;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(50, 50);
                    var d = Dust.NewDustPerfect(pos, DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 4f;
                }
                return;
            }
            NPC.dontTakeDamage = false;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                Vector2 toOwner = (Owner.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                NPC.velocity = Vector2.Lerp(NPC.velocity, toOwner * 8f, 0.05f);
            }
            else {
                RunCombatAI(target);
            }

            CreateAmbientParticles();
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreCyan.ToVector3() * 0.3f);
        }

        private void RunCombatAI(Player target) {
            switch (lungeState) {
                case 0: // 游荡 + 远程 (带最小射距)
                    float orbitRadius = 180f + MathF.Sin(pulsePhase * 0.5f) * 30f;
                    Vector2 targetPos = target.Center + new Vector2(
                        MathF.Cos(orbitAngle + NPC.whoAmI * MathHelper.PiOver2) * orbitRadius,
                        MathF.Sin(orbitAngle + NPC.whoAmI * MathHelper.PiOver2) * orbitRadius * 0.6f
                    );
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center) * 0.08f, 0.06f);

                    // 贴身可压制: 260px 内不射击 (风险邀请)
                    if (attackTimer % 90 == 0 && NPC.Distance(target.Center) > MinFireDist)
                        ShootAtTarget(target);

                    if (attackTimer % LungeInterval == LungeInterval - 1) {
                        lungeState = 1;
                        lungeTimer = 0;
                    }
                    break;

                case 1: // 预告: 减速锁向 (青白预告线见 PreDraw), 尾段轻微后仰蓄势
                    NPC.velocity *= 0.88f;
                    lungeDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    lungeTimer++;
                    if (lungeTimer > LungeWindup - 10)
                        NPC.velocity -= lungeDir * 0.8f; // counter-motion 吸气
                    if (!Main.dedServ && lungeTimer % 3 == 0) {
                        var d = Dust.NewDustPerfect(NPC.Center + lungeDir * 30f, DustID.IceTorch);
                        d.noGravity = true;
                        d.scale = 1.1f;
                        d.velocity = lungeDir * 3f;
                    }
                    if (lungeTimer >= LungeWindup) {
                        lungeState = 2;
                        lungeTimer = 0;
                        NPC.velocity = lungeDir * 22f; // 单帧 launch
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.4f, Volume = 0.7f }, NPC.Center);
                    }
                    break;

                case 2: // 俯冲: 直线零转向, 速度门控拖尾
                    lungeTimer++;
                    if (!Main.dedServ)
                        SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.3f);
                    if (lungeTimer >= 22) {
                        lungeState = 3;
                        lungeTimer = 0;
                    }
                    break;

                case 3: // 硬刹 ×0.72 → 回游荡
                    NPC.velocity *= 0.72f;
                    lungeTimer++;
                    if (lungeTimer >= 16)
                        lungeState = 0;
                    break;
            }

            NPC.rotation = NPC.velocity.X * 0.04f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1); // 冤魂接触挂魂蚀
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 俯冲预告线 (青白, 末 10f 转红 = 致命窗口)
            if (lungeState == 1) {
                float prog = lungeTimer / (float)LungeWindup;
                bool imminent = lungeTimer >= LungeWindup - 10;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                Color edge = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + lungeDir * 380f,
                    MathHelper.Lerp(4f, 10f, prog), core, edge, 0.3f + 0.5f * prog);
            }
            return DrawMinionBody(spriteBatch, screenPos);
        }

        private void ShootAtTarget(Player target) {
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.7f }, NPC.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toTarget * 8f,
                ModContent.ProjectileType<SpectreSoulOrb>(), NPC.damage / 2, 1f,
                ai0: Main.rand.Next(2));
        }

        private void CreateAmbientParticles() {
            if (Main.rand.NextBool(4)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20, 20), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = Main.rand.NextVector2Circular(1, 1);
                d.alpha = 120;
            }
        }

        private bool DrawMinionBody(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Color tint = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f);
            float speedGate = Utils.GetLerpValue(3f, 12f, NPC.velocity.Length(), true);
            float dashBlur = lungeState == 2 ? 1f : 0f;
            Vector2 dashUV = lungeState >= 2 ? lungeDir : Vector2.Zero;

            // 与本体同一鬼相语言 (SpectreVeil): 凝形溶解 + 虚相残影
            if (SpectreHelper.BeginVeilBatch(spriteBatch)) {
                if (speedGate > 0.05f) {
                    for (int i = 6; i >= 2; i -= 2) {
                        if (i >= NPC.oldPos.Length || NPC.oldPos[i] == Vector2.Zero) continue;
                        float prog = 1f - i / 8f;
                        SpectreHelper.ApplyVeilParams(0.8f, spawnDissolve, 0.25f * prog * speedGate, 0f,
                            dashUV, dashBlur * 0.5f, tint, 0.4f, SpectreHelper.SpectreGhostFlame, 0.6f);
                        spriteBatch.Draw(tex, NPC.oldPos[i] + NPC.Size / 2 - screenPos, null, Color.White,
                            NPC.rotation, origin, NPC.scale * (0.5f + prog * 0.5f), SpriteEffects.None, 0);
                    }
                }

                SpectreHelper.ApplyVeilParams(0.25f, spawnDissolve, 1f, lungeState == 1 ? 0.9f : 0.4f,
                    dashUV, dashBlur, tint, 0.4f, SpectreHelper.SpectreGhostFlame, 0.8f);
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, Color.White,
                    NPC.rotation, origin, NPC.scale * pulse, SpriteEffects.None, 0);

                SpectreHelper.EndVeilBatch(spriteBatch);
            }
            else {
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, tint * (1f - spawnDissolve),
                    NPC.rotation, origin, NPC.scale * pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill() {
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 50f, 2, 10);

            // 清账正反馈: 怨念 -3 (服务器权威)
            if (Main.netMode != NetmodeID.MultiplayerClient && Owner != null)
                UnderworldField.ReduceGrudge(Owner, 3);

            if (Main.netMode != NetmodeID.Server) {
                // 魂缕飞向最近灯笼 — 清账的可视化
                Vector2 sink = NPC.Center + new Vector2(0, -160f);
                int lanternType = ModContent.ProjectileType<SpectreLanternAnchor>();
                float best = float.MaxValue;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != lanternType) continue;
                    float d2 = p.DistanceSQ(NPC.Center);
                    if (d2 < best) {
                        best = d2;
                        sink = p.Center;
                    }
                }
                for (int i = 0; i < 14; i++) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(16, 16),
                        Main.rand.NextBool() ? DustID.IceTorch : DustID.GreenTorch);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = (sink - NPC.Center).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(4f, 9f)
                        + Main.rand.NextVector2Circular(1.5f, 1.5f);
                }
            }
        }

        public override bool CheckActive() {
            // 宿主存在时不自动消失
            return Owner == null || !Owner.active;
        }
    }
}

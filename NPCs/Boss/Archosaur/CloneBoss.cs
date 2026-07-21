using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 残魂幻影头 (V3) — 从本体"撕出"的半透明灰蓝雷龙, 破绽钥匙 (被击破 → 宿主逆雷 + 破绽窗口)。
    /// 读法语言: 幻影 = 灰蓝 + 静电撕裂 + 半透明 (ArchosaurPhantom 着色器); 其俯冲是真伤害, 预警线照常转红。
    /// 单 striker 节流: 仅当宿主处于低压招式时才起手俯冲, 其余时间外围环绕 (presence 而非 pressure)。
    /// ai[0] = 子状态; ai[3] = 宿主 whoAmI。受伤无减免 (realLife 非 ArchosaurHead → 不减伤)。
    /// </summary>
    public class CloneBossHead : ArchosaurBoss
    {
        private static readonly SoundStyle DiveSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurSummon") { Volume = 0.6f, PitchVariance = .2f, MaxInstances = 3 };

        public override WormType NPCWormType => WormType.Head;
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur_Head";

        // ===== 子状态 (ai[0]; S_Cross/S_Merge/S_Vanish 由宿主指令写入) =====
        public const int S_Birth = 0, S_Orbit = 1, S_Repos = 2, S_DiveTele = 3, S_Dive = 4,
            S_Cross = 5, S_Merge = 6, S_Vanish = 7;

        private const int BirthTicks = 60, ReposMax = 60, TeleTicks = 36, DiveTicks = 44, CrossLaunch = 70;

        /// <summary>溶解可视 (0=实体 1=消散; 出生 1→0, 吸收/消散 0→1)。段节绘制读取。</summary>
        public float DissolveVisual { get; private set; } = 1f;

        private int lastState = -1;
        private Vector2 diveDir = Vector2.UnitX;
        private float dashTele;
        private int diveCooldown = 120;
        private float segContactMult = 0.3f;
        public override float SegmentContactMult => segContactMult;

        private ref float State => ref NPC.ai[0];
        private ref float StateTimer => ref NPC.localAI[0];
        private ref float OrbitPhase => ref NPC.localAI[1];

        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody2>();

        /// <summary>
        /// 故意不调用 base.OnSpawn: 阻断 <see cref="BasicWorm"/> 的父链继承 —
        /// 幻影由宿主 AI 生成 (EntitySource_FromAI 属 EntitySource_Parent), 若走 base 会把 realLife
        /// 指到宿主头, 与本体共享血池, 破绽钥匙机制即失效。幻影必须是独立蠕虫。
        /// </summary>
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.lifeMax = 130000;   // 可被快速击破 (破绽钥匙, 非血墙)
            NPC.defense = 60;
            NPC.damage = 220;
            SummonMax = 12;         // 短残躯, 区别于宿主长身
        }

        private ArchosaurHead Host {
            get {
                int hi = (int)NPC.ai[3];
                if (hi >= 0 && hi < Main.maxNPCs && Main.npc[hi].active && Main.npc[hi].ModNPC is ArchosaurHead h)
                    return h;
                return null;
            }
        }

        public override void AI() {
            base.AI();

            Player target = Target;
            bool server = Main.netMode != NetmodeID.MultiplayerClient;
            ArchosaurHead host = Host;
            Vector2 hostCenter = host?.NPC.Center ?? (target.Center - Vector2.UnitY * 400f);

            if ((!target.active || target.dead) && (int)State != S_Vanish) {
                NPC.velocity.Y -= 0.4f;
                NPC.EncourageDespawn(30);
                return;
            }
            // 宿主消失 (脱战/被清) → 幻影不独活
            if (host == null && (int)State != S_Vanish && server) {
                State = S_Vanish;
                NPC.netUpdate = true;
            }

            // 子状态切换时本地复位计时器 (ai[0] 同步, localAI 各端确定性自增)
            if ((int)State != lastState) {
                lastState = (int)State;
                StateTimer = 0;
                dashTele = 0f;
            }
            StateTimer++;
            float t = StateTimer;

            switch ((int)State) {
                case S_Birth: DoBirth(server, target, hostCenter, t); break;
                case S_Orbit: DoOrbit(server, target, host, t); break;
                case S_Repos: DoRepos(server, target, hostCenter, t); break;
                case S_DiveTele: DoDiveTele(server, target, t); break;
                case S_Dive: DoDive(server, target, t); break;
                case S_Cross: DoCross(server, target, hostCenter, t); break;
                case S_Merge: DoMerge(server, target, hostCenter, host, t); break;
                case S_Vanish: DoVanish(server, t); break;
                default: DoOrbit(server, target, host, t); break;
            }

            // 常态子状态下溶解自然归零 (出生被打断/被指令抢占时不会卡在半透)
            if ((int)State is S_Orbit or S_Repos or S_DiveTele or S_Dive or S_Cross)
                DissolveVisual = MathHelper.Lerp(DissolveVisual, 0f, 0.08f);

            // 无敌与接触门控: 出生/归一/消散是演出, 不是可乘之机也不是威胁
            bool protectedState = (int)State is S_Merge or S_Vanish || ((int)State == S_Birth && t < 50);
            NPC.dontTakeDamage = protectedState;
            UpdateContactGate(protectedState);

            dashTele *= 0.85f;
            if (diveCooldown > 0)
                diveCooldown--;

            if ((int)State != S_Dive && (int)State != S_Cross)
                FaceVelocityClone();
        }

        // —— 出生: 从本体被"拽出", 加速甩向对侧, 边行进边显形 ——
        private void DoBirth(bool server, Player target, Vector2 hostCenter, float t) {
            if (t == 1) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.3f }, NPC.Center);
                ArchosaurStormSystem.AddFlash(0.5f);
                ACMUtils.AddScreenShake(5f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 22; i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f),
                            DustID.Electric, Main.rand.NextVector2Circular(5f, 5f), 90, ArchosaurVFX.PhantomBlue, 1.4f);
                        d.noGravity = true;
                    }
                }
            }
            DissolveVisual = MathHelper.Clamp(1f - t / 45f, 0f, 1f);
            Vector2 dir = (target.Center - hostCenter).SafeNormalize(Vector2.UnitX);
            Vector2 goal = target.Center + dir * 540f;
            float speed = MathHelper.Lerp(6f, 26f, MathHelper.Clamp(t / 40f, 0f, 1f));
            NPC.velocity = Vector2.Lerp(NPC.velocity, (goal - NPC.Center).SafeNormalize(Vector2.UnitX) * speed, 0.1f);
            if (t >= BirthTicks && server) {
                State = S_Orbit;
                NPC.netUpdate = true;
            }
        }

        // —— 环绕 (presence): 外围大圈游弋; 宿主低压时才允许起手俯冲 (单 striker 阀门) ——
        private void DoOrbit(bool server, Player target, ArchosaurHead host, float t) {
            OrbitPhase += 0.016f;
            Vector2 desired = target.Center + OrbitPhase.ToRotationVector2() * 620f;
            Vector2 want = desired - NPC.Center;
            float cap = Math.Min(want.Length() * 0.15f, 22f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, want.SafeNormalize(Vector2.Zero) * cap, 0.08f);

            if (server && t > 70 && diveCooldown <= 0 && (host?.InLowPressureState ?? true)) {
                State = S_Repos;
                NPC.netUpdate = true;
            }
        }

        // —— 换位: 绕到玩家相对宿主的另一侧 (到位早退) ——
        private void DoRepos(bool server, Player target, Vector2 hostCenter, float t) {
            Vector2 dir = (target.Center - hostCenter).SafeNormalize(Vector2.UnitX);
            Vector2 goal = target.Center + dir * 540f;
            Vector2 want = goal - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, want.SafeNormalize(Vector2.Zero) * Math.Min(want.Length() * 0.2f, 28f), 0.12f);
            if (server && (want.Length() < 80f || t >= ReposMax)) {
                State = S_DiveTele;
                NPC.netUpdate = true;
            }
        }

        // —— 俯冲预告: 倒吸蓄势 + 红线 (线在发射前 8f 锁定 = 躲避窗口) ——
        private void DoDiveTele(bool server, Player target, float t) {
            if (t < TeleTicks - 8)
                diveDir = (target.Center + target.velocity * 9f - NPC.Center).SafeNormalize(Vector2.UnitX);
            float bt = MathHelper.Clamp(t / TeleTicks, 0f, 1f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, -diveDir * MathF.Pow(bt, 8f) * 14f, 0.3f);
            dashTele = bt;
            NPC.spriteDirection = diveDir.X >= 0 ? 1 : -1;
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                Dust d = Dust.NewDustPerfect(from, DustID.Electric, (NPC.Center - from) * 0.07f, 120, ArchosaurVFX.PhantomBlue, 1.1f);
                d.noGravity = true;
            }
            if (t >= TeleTicks && server) {
                State = S_Dive;
                NPC.netUpdate = true;
            }
        }

        // —— 俯冲: 瞬发 46px/f ×1.02 复利 → 硬刹 ——
        private void DoDive(bool server, Player target, float t) {
            if (t == 1) {
                NPC.velocity = diveDir * 46f;
                dashTele = 0f;
                SoundEngine.PlaySound(DiveSfx, NPC.Center);
                ACMUtils.AddScreenShake(4f);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocityClone();
            }
            else if (t < DiveTicks) {
                NPC.velocity *= 1.02f;
                FaceVelocityClone();
                if (!Main.dedServ) {
                    Dust d = Dust.NewDustDirect(NPC.Center - new Vector2(8), 16, 16, DustID.Electric, 0f, 0f, 80, ArchosaurVFX.PhantomBlue, 1.3f);
                    d.noGravity = true;
                }
            }
            else {
                NPC.velocity *= 0.75f;
                if (t >= DiveTicks + 16 && server) {
                    State = S_Orbit;
                    NPC.netUpdate = true;
                }
            }
            if (t >= DiveTicks)
                diveCooldown = 160;
        }

        // —— 双龙对冲 (宿主指令): 对角异高就位 → 红线 → 比宿主晚 ~14f 错拍冲刺 ——
        private void DoCross(bool server, Player target, Vector2 hostCenter, float t) {
            float side = -MathF.Sign(hostCenter.X - target.Center.X + 0.01f);   // 与宿主异侧
            Vector2 anchor = target.Center + new Vector2(side * 520f, -140f);   // 与宿主异高 (逃逸缝)

            if (t < 36) {
                Vector2 want = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * 30f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.15f);
                FaceVelocityClone();
            }
            else if (t < CrossLaunch) {
                float bt = (t - 36f) / (CrossLaunch - 36f);
                if (t < CrossLaunch - 8)
                    diveDir = (target.Center + target.velocity * 9f - NPC.Center).SafeNormalize(Vector2.UnitX);
                Vector2 reel = anchor - diveDir * MathF.Pow(bt, 8f) * 170f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (reel - NPC.Center) * 0.2f, 0.3f);
                dashTele = MathHelper.Clamp((t - 36f) / 16f, 0f, 1f);
                NPC.spriteDirection = diveDir.X >= 0 ? 1 : -1;
            }
            else if (t == CrossLaunch) {
                NPC.velocity = diveDir * 46f;
                dashTele = 0f;
                SoundEngine.PlaySound(DiveSfx, NPC.Center);
                ACMUtils.AddScreenShake(5f);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocityClone();
            }
            else if (t < CrossLaunch + 14) {
                NPC.velocity *= 1.02f;
                FaceVelocityClone();
            }
            else {
                NPC.velocity *= 0.75f;
                if (t >= 112 && server) {
                    diveCooldown = 160;
                    State = S_Orbit;
                    NPC.netUpdate = true;
                }
            }
        }

        // —— 归一 (P3 相变): 化光流回本体, 触及即散为金屑 ——
        private void DoMerge(bool server, Player target, Vector2 hostCenter, ArchosaurHead host, float t) {
            float dist = Vector2.Distance(NPC.Center, hostCenter);
            float speed = Math.Min(8f + t * 0.5f, 40f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hostCenter - NPC.Center).SafeNormalize(Vector2.UnitX) * speed, 0.15f);
            DissolveVisual = MathHelper.Clamp(1f - dist / 400f, 0f, 1f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.GoldCoin, NPC.velocity * 0.2f, 0, default, 1.1f);
                d.noGravity = true;
            }
            if ((dist < 70f || host == null || t > 240) && server)
                SilentDespawn();
        }

        // —— 消散 (宿主死亡剧本): 原地化光屑散去 ——
        private void DoVanish(bool server, float t) {
            NPC.velocity *= 0.9f;
            DissolveVisual = MathHelper.Clamp(t / 30f, 0f, 1f);
            if (t >= 32 && server)
                SilentDespawn();
        }

        /// <summary>静默移除 (不触发 OnKill → 不触发宿主逆雷); 段节随父链自动消失。</summary>
        private void SilentDespawn() {
            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 24f),
                        DustID.GoldCoin, Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }
            NPC.active = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
        }

        private void UpdateContactGate(bool protectedState) {
            if (scaledContact < 0)
                return;
            float speed = NPC.velocity.Length();
            float headMult, segMult;
            if (protectedState) {
                headMult = 0f;
                segMult = 0f;
            }
            else if ((int)State is S_Dive or S_Cross && speed > 22f) {
                headMult = 1f;
                segMult = 0.7f;
            }
            else {
                headMult = 0.3f;
                segMult = 0.3f;
            }
            segContactMult = segMult;
            NPC.damage = (int)(scaledContact * headMult);
        }

        private void FaceVelocityClone() {
            if (NPC.velocity.LengthSquared() < 0.3f)
                return;
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }

        public override void OnKill() {
            // 被玩家击破 → 宿主在 AI 中观测幻影消失, 触发逆雷 + 破绽窗口
            SoundEngine.PlaySound(SoundID.NPCDeath56 with { Volume = 0.8f }, NPC.Center);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 26; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 60, ArchosaurVFX.PhantomBlue, 1.6f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            // 俯冲红线 (幻影俯冲是真伤害 → 预警语义跟伤害走, 照常转红; 线更细以保双龙同屏可读)
            if (dashTele > 0.03f) {
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + diveDir * 950f,
                    MathHelper.Lerp(1.5f, 4.5f, dashTele), TelegraphColors.Lethal, TelegraphColors.Lethal * 0.25f,
                    dashTele * 0.7f, flowSpeed: 2.6f, flowScale: 2.6f);
            }

            // 幻影显形着色器: 灰蓝去饱和 + 静电撕裂 + 溶解
            Effect fx = ArchosaurVFX.Phantom;
            if (fx != null) {
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uSeed"]?.SetValue((NPC.whoAmI * 0.137f) % 1f);
                fx.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(DissolveVisual, 0f, 1f));
                fx.Parameters["uGlitch"]?.SetValue(0.35f + 0.65f * dashTele);
                fx.Parameters["uOpacity"]?.SetValue(0.92f);
                fx.Parameters["uTint"]?.SetValue(ArchosaurVFX.PhantomBlue.ToVector4());

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
                DrawSegmentSprite(spriteBatch, screenPos, Color.White);
                spriteBatch.End();
                ACMShaders.RestoreDefaultBatch(spriteBatch);
            }
            else {
                DrawSegmentSprite(spriteBatch, screenPos,
                    Color.Lerp(drawColor, ArchosaurVFX.PhantomBlue, 0.5f) * (1f - DissolveVisual));
            }

            // 灰蓝凝视辉光 (蓄力渐强; 与真身金瞳形成读法对照)
            Texture2D g = ACMAsset.SoftGlow;
            if (g != null && DissolveVisual < 0.9f) {
                Color c = ArchosaurVFX.PhantomBlue with { A = 0 };
                float scale = (1.0f + 0.5f * dashTele) * (0.6f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f));
                spriteBatch.Draw(g, NPC.Center - screenPos, null, c * ((0.35f + 0.65f * dashTele) * (1f - DissolveVisual)),
                    0f, g.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    public class CloneBossBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody2>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
            NPC.height = 50;
            NPC.damage = 150;
        }
    }
    public class CloneBossBody2 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<CloneBossBody2>();
            if (SummonCount == SummonMax / 3 * 2 || SummonCount == 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody1>();
            if (SummonCount > SummonMax - 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody3>();
        }
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
            NPC.damage = 150;
        }
    }
    public class CloneBossBody3 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody4>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
            NPC.damage = 150;
        }
    }
    public class CloneBossBody4 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossTail>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
            NPC.damage = 150;
        }
    }
    public class CloneBossTail : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Tail;
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
            NPC.damage = 150;
        }
    }
}

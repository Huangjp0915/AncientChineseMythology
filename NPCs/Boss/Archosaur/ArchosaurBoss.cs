using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    public abstract class ArchosaurBoss : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/" + Name;

        public override bool IsUseSpriteDirection => true;

        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.height = 80;
            NPC.lifeMax = 500000;
            NPC.damage = 1000;
            NPC.defense = 300;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0;
            SummonMax = 80;
        }

        public override void AI() {
            base.AI();
            // V2: 不再有「分身存活=本体无敌」反模式。仅在相变 i-frame 节拍由头部驱动, 段节随宿主头同步。
            if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                NPC h = Main.npc[NPC.realLife];
                if (h.active && h.ModNPC is ArchosaurHead)
                    NPC.dontTakeDamage = h.dontTakeDamage;
            }
        }

        /// <summary>
        /// V2 破绽窗口: 宿主本体在替身存活时受伤减半(50%), 替身被破后的逆雷/破绽窗口期受伤加成。
        /// 段节命中按 realLife(宿主头) 的当前倍率结算 (替身蠕虫的 realLife 非 ArchosaurHead → 不减伤, 可被快速击破)。
        /// </summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            NPC host = NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs ? Main.npc[NPC.realLife] : NPC;
            if (host.active && host.ModNPC is ArchosaurHead head)
                modifiers.FinalDamage *= head.DamageTakenMult;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            _ = TextureAssets.Npc[Type].Value;
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new(NPC.spriteDirection == -1 ? 0 : tex.Width, 20);
            if (NPCWormType == WormType.Head) {
                origin.Y += 34;
                origin.X = NPC.spriteDirection == -1 ? (tex.Width / 4) : (tex.Width / 4 * 3);
            }
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, NPC.scale, NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }

    [AutoloadBossHead]
    public class ArchosaurHead : ArchosaurBoss
    {
        private static readonly SoundStyle SummonSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurSummon") { Volume = 1f, PitchVariance = .12f, MaxInstances = 5 };
        private static readonly SoundStyle DeathSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurDeath") { Volume = 1f, PitchVariance = .04f, MaxInstances = 3 };

        private const string BattleMusicPath = "AncientChineseMythology/Sounds/Archosaur/ArchosaurBattle";

        public override WormType NPCWormType => WormType.Head;
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur_Head";

        // ===== 演出层 (供 ArchosaurStormSystem / PreDraw 读取; 纯本地视觉) =====
        public static int ActiveHead = -1;
        public float StormVisual { get; private set; }
        public float ChargeGlow { get; private set; }
        public float WindowVisual { get; private set; }
        public Vector2 NestCenter { get; private set; }

        /// <summary>当前受伤倍率 (破绽窗口机制): 1=常态, 0.5=替身存活, &gt;1=破绽窗口。</summary>
        public float DamageTakenMult { get; private set; } = 1f;

        // ===== 攻击编号 (NPC.ai[2]) =====
        private const int A_Roam = 0, A_Volley = 1, A_Orbiting = 2, A_TailLightning = 3, A_Nest = 4, A_Reverse = 5, A_Window = 6;
        private static readonly int[] Rotation = { A_Volley, A_Orbiting, A_TailLightning, A_Nest };

        // 时序常量 (tick)
        private const int RoamTicks = 55;
        private const int VolleyCharge = 90, VolleyRecover = 30;       // 1.5s 聚能
        private const int OrbitGlow = 120, OrbitDive = 34, OrbitRecover = 26;
        private const int TailWarm = 22, TailInterval = 14, TailMax = 16;
        private const int NestCast = 54;
        private const int ReverseTicks = 100;
        private const int WindowTicks = 360;
        private const int CloneCooldownTicks = 60 * 11;

        // 同步状态
        private ref float Phase => ref NPC.ai[0];        // 0=P1 1=P2
        private ref float Attack => ref NPC.ai[2];       // AttackId
        private ref float CloneIdx => ref NPC.ai[3];     // 替身头 whoAmI (-1 无)
        // 本地状态 (非同步; 转移由 ai[]+netUpdate 驱动, 计时器在各端确定性自增)
        private ref float StateTimer => ref NPC.localAI[0];
        private ref float Fig8 => ref NPC.localAI[1];

        private bool initialized;
        private int lastAttack = -1;
        private int rotationIndex = -1;
        private bool cloneWasAlive;
        private int cloneCooldown;
        private int iFrames;
        private int tailFired;
        private int tailNextTimer;
        private List<int> tailSegments;
        private Vector2 diveDir;

        public override void SetStaticDefaults() {
            NPCID.Sets.ShouldBeCountedAsBoss[NPC.type] = true;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
            Music = MusicLoader.GetMusicSlot(Mod, BattleMusicPath);
            SceneEffectPriority = SceneEffectPriority.BossHigh;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
        }

        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody2>();

        public override void OnSpawn(IEntitySource source) => SoundEngine.PlaySound(SummonSfx, NPC.Center);

        public override void OnKill() {
            DownedBossSystem.downedArchosaur = true;
            ActiveHead = -1;
            SoundEngine.PlaySound(DeathSfx, NPC.Center);
            ACMUtils.AddScreenShake(14f);
        }

        public override void AI() {
            ActiveHead = NPC.whoAmI;
            Player target = Target;
            bool server = Main.netMode != NetmodeID.MultiplayerClient;

            if (!initialized) {
                initialized = true;
                if (Attack == 0 && StateTimer == 0)
                    Attack = A_Roam;
            }

            // 相变: 60% → P2「双生雷相」, 给一次相变 i-frame 节拍 (仅转移处)
            if (Phase == 0 && NPC.life <= NPC.lifeMax * 0.6f) {
                Phase = 1f;
                iFrames = 45;
                StormVisual = Math.Max(StormVisual, 0.5f);
                ACMUtils.AddScreenShake(11f);
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                if (server) {
                    SpawnClone(target);
                    Attack = A_Roam;
                    StateTimer = 0;
                    cloneCooldown = CloneCooldownTicks;
                    NPC.netUpdate = true;
                }
            }

            if (Phase >= 1f)
                HandleCloneCycle(server);

            // 客户端: 攻击同步切换时本地复位计时器/瞬态, 与服务器转移对齐 (localAI 不走同步)
            if ((int)Attack != lastAttack) {
                lastAttack = (int)Attack;
                StateTimer = 0;
                tailSegments = null;
            }

            ChargeGlow = MathHelper.Lerp(ChargeGlow, 0f, 0.1f);

            StateTimer++;
            RunAttack(server, target);

            UpdateDamageMult();
            UpdateVisuals();

            if (iFrames > 0) { iFrames--; NPC.dontTakeDamage = true; }
            else NPC.dontTakeDamage = false;
        }

        // ===========================================================
        //  P2 替身循环 (破绽窗口)
        // ===========================================================
        private void HandleCloneCycle(bool server) {
            bool cloneAlive = CloneAlive(out _);
            if (cloneAlive) {
                cloneWasAlive = true;
                return;
            }
            if (cloneWasAlive) {
                // 替身刚被击破 → 逆雷 + 破绽窗口
                cloneWasAlive = false;
                CloneIdx = -1;
                ACMUtils.AddScreenShake(8f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f }, NPC.Center);
                cloneCooldown = CloneCooldownTicks;
                if (server) {
                    Attack = A_Reverse;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
                return;
            }
            // 无替身常态: 倒计时后重新分裂 (不在逆雷/破绽窗口期间)
            if (Attack != A_Reverse && Attack != A_Window) {
                if (cloneCooldown > 0)
                    cloneCooldown--;
                else if (server)
                    SpawnClone(Target);
            }
        }

        private bool CloneAlive(out NPC clone) {
            clone = null;
            int i = (int)CloneIdx;
            if (i < 0 || i >= Main.maxNPCs)
                return false;
            NPC n = Main.npc[i];
            if (n.active && n.ModNPC is CloneBossHead) {
                clone = n;
                return true;
            }
            return false;
        }

        // ===========================================================
        //  攻击状态机
        // ===========================================================
        private void RunAttack(bool server, Player target) {
            switch ((int)Attack) {
                case A_Roam: DoRoam(server, target); break;
                case A_Volley: DoVolley(server, target); break;
                case A_Orbiting: DoOrbitingCharge(server, target); break;
                case A_TailLightning: DoTailLightning(server, target); break;
                case A_Nest: DoNest(server, target); break;
                case A_Reverse: DoReverse(server, target); break;
                case A_Window: DoWindow(server, target); break;
                default: DoRoam(server, target); break;
            }
        }

        private void DoRoam(bool server, Player target) {
            HoverMovement(target, 1f);
            if (server && StateTimer >= RoamTicks)
                NextAttack(server);
        }

        // —— 残雷齐射: 头部聚能 1.5s (粒子汇聚) → 可读扇形直射, 无自残 ——
        private void DoVolley(bool server, Player target) {
            if (StateTimer < VolleyCharge) {
                HoverMovement(target, 0.45f);
                float p = StateTimer / (float)VolleyCharge;
                ChargeGlow = Math.Max(ChargeGlow, 0.25f + 0.75f * p);
                ConvergeParticles(p);
            }
            if (StateTimer == VolleyCharge) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.2f }, NPC.Center);
                ACMUtils.AddScreenShake(6f);
                if (server)
                    FireVolley(target);
            }
            if (StateTimer >= VolleyCharge) {
                HoverMovement(target, 0.7f);
                if (StateTimer >= VolleyCharge + VolleyRecover)
                    EndToRoam(server);
            }
        }

        // —— 盘旋蓄势 2s (头部发光) → 沿速度线俯冲 ——
        private void DoOrbitingCharge(bool server, Player target) {
            if (StateTimer < OrbitGlow) {
                HoverMovement(target, 0.4f);
                float p = StateTimer / (float)OrbitGlow;
                ChargeGlow = Math.Max(ChargeGlow, 0.2f + 0.8f * p);
                diveDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            }
            else if (StateTimer == OrbitGlow) {
                NPC.velocity = diveDir * 30f;
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1) NPC.rotation += MathHelper.Pi;
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
            }
            else if (StateTimer < OrbitGlow + OrbitDive) {
                NPC.velocity *= 1.005f;
                FaceVelocity();
                if (!Main.dedServ) {
                    Dust d = Dust.NewDustDirect(NPC.Center - new Vector2(10), 20, 20, DustID.Electric, 0f, 0f, 60, default, 1.5f);
                    d.noGravity = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                HoverMovement(target, 0.5f);
                if (StateTimer >= OrbitGlow + OrbitDive + OrbitRecover)
                    EndToRoam(server);
            }
        }

        // —— 尾雷: 龙身段节依序释放纵向落雷 (身体成为机制载体) ——
        private void DoTailLightning(bool server, Player target) {
            HoverMovement(target, 0.85f);
            if (StateTimer == 0 || tailSegments == null) {
                tailSegments = server ? GatherSegments() : null;
                tailFired = 0;
                tailNextTimer = TailWarm;
            }
            if (server && StateTimer >= TailWarm) {
                if (--tailNextTimer <= 0 && tailFired < TailMax && tailSegments != null && tailSegments.Count > 0) {
                    tailNextTimer = TailInterval;
                    int seg = tailSegments[tailFired % tailSegments.Count];
                    if (seg >= 0 && seg < Main.maxNPCs && Main.npc[seg].active) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), Main.npc[seg].Center, Vector2.Zero,
                            ModContent.ProjectileType<ArchosaurTailBolt>(), 120, 0f, Main.myPlayer, seg, 40f);
                    }
                    tailFired++;
                }
                int fireCount = Math.Min(TailMax, Math.Max(6, tailSegments?.Count ?? 6));
                if (tailFired >= fireCount)
                    EndToRoam(server);
            }
            // 安全超时: 即便无可用段节也不会卡死本状态
            if (server && StateTimer > TailWarm + TailInterval * (TailMax + 3))
                EndToRoam(server);
        }

        // —— 雷巢: 三角阵静态雷球 + 中点可破链电 ——
        private void DoNest(bool server, Player target) {
            HoverMovement(target, 0.9f);
            if (StateTimer == 4) {
                if (server)
                    SpawnNest(target);
                ACMUtils.AddScreenShake(5f);
            }
            if (StateTimer >= NestCast)
                EndToRoam(server);
        }

        // —— 逆雷 (替身被破触发): 外环向心汇聚, 躲位向外 ——
        private void DoReverse(bool server, Player target) {
            HoverMovement(target, 0.45f);
            WindowVisual = Math.Max(WindowVisual, 0.4f);
            if (StateTimer == 2) {
                if (server)
                    SpawnReverse(target);
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(7f);
            }
            if (StateTimer >= ReverseTicks && server) {
                Attack = A_Window;
                StateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        // —— 破绽窗口: 本体全力暴露(受伤加成) + 减速 + 龙身高亮 ——
        private void DoWindow(bool server, Player target) {
            HoverMovement(target, 0.4f);
            ChargeGlow = Math.Max(ChargeGlow, 0.6f);
            if (StateTimer >= WindowTicks)
                EndToRoam(server);
        }

        private void NextAttack(bool server) {
            if (!server)
                return;
            rotationIndex = (rotationIndex + 1) % Rotation.Length;
            Attack = Rotation[rotationIndex];
            StateTimer = 0;
            tailSegments = null;
            NPC.netUpdate = true;
        }

        private void EndToRoam(bool server) {
            if (!server)
                return;
            Attack = A_Roam;
            StateTimer = 0;
            NPC.netUpdate = true;
        }

        // ===========================================================
        //  生成器 (服务器权威)
        // ===========================================================
        private void FireVolley(Player target) {
            const int count = 9;
            Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float spread = MathHelper.ToRadians(64f);
            for (int i = 0; i < count; i++) {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                Vector2 dir = toP.RotatedBy(MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, t));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 13f,
                    ModContent.ProjectileType<ArchosaurStormOrb>(), 90, 0f, Main.myPlayer);
            }
        }

        private void SpawnNest(Player target) {
            NestCenter = target.Center;
            const float radius = 270f;
            Vector2[] pts = new Vector2[3];
            for (int i = 0; i < 3; i++) {
                float ang = MathHelper.PiOver2 + i * MathHelper.TwoPi / 3f;
                pts[i] = target.Center + ang.ToRotationVector2() * radius;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), pts[i], Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurNestOrb>(), 0, 0f, Main.myPlayer);
            }
            for (int i = 0; i < 3; i++) {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % 3];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 half = (b - a) * 0.5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), mid, Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurNestLink>(), 130, 0f, Main.myPlayer, half.X, half.Y);
            }
        }

        private void SpawnReverse(Player target) {
            Vector2 center = target.Center;
            const int count = 11;
            const float radius = 740f;
            for (int i = 0; i < count; i++) {
                float ang = i * MathHelper.TwoPi / count + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 pos = center + ang.ToRotationVector2() * radius;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurReverseBolt>(), 110, 0f, Main.myPlayer,
                    center.X, center.Y, 36f);
            }
        }

        private void SpawnClone(Player target) {
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            Vector2 pos = target.Center + dir * 560f; // 玩家另一侧
            int id = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y, ModContent.NPCType<CloneBossHead>());
            if (id >= 0 && id < Main.maxNPCs) {
                Main.npc[id].ai[3] = NPC.whoAmI; // 替身记录宿主
                CloneIdx = id;
                cloneWasAlive = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: id);
            }
            NPC.netUpdate = true;
        }

        // ===========================================================
        //  辅助
        // ===========================================================
        private List<int> GatherSegments() {
            List<int> list = new();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.realLife != NPC.whoAmI)
                    continue;
                if (n.ModNPC is ArchosaurBoss bw && bw.NPCWormType != WormType.Head)
                    list.Add(i);
            }
            list.Sort((x, y) => {
                int sx = (Main.npc[x].ModNPC as BasicWorm)?.SummonCount ?? 0;
                int sy = (Main.npc[y].ModNPC as BasicWorm)?.SummonCount ?? 0;
                return sx.CompareTo(sy);
            });
            return list;
        }

        private void UpdateDamageMult() {
            float m = 1f;
            if (Phase >= 1f) {
                if ((int)Attack == A_Window) m = 1.6f;
                else if ((int)Attack == A_Reverse) m = 1.2f;
                else if (CloneAlive(out _)) m = 0.5f;
            }
            DamageTakenMult = m;
        }

        private void UpdateVisuals() {
            float stormTarget = Phase >= 1f ? 0.7f : 0.42f;
            if ((int)Attack == A_Window) stormTarget = 1f;
            StormVisual = MathHelper.Lerp(StormVisual, stormTarget, 0.02f);
            WindowVisual = MathHelper.Lerp(WindowVisual, (int)Attack == A_Window ? 1f : 0f, 0.05f);
        }

        private void ConvergeParticles(float p) {
            if (Main.dedServ)
                return;
            int n = 2 + (int)(p * 3);
            for (int i = 0; i < n; i++) {
                Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(140f, 140f) * (1.1f - p * 0.6f);
                Dust d = Dust.NewDustPerfect(from, DustID.Electric, (NPC.Center - from) * 0.06f, 80, default, 1.2f);
                d.noGravity = true;
            }
        }

        // 8 字 Lissajous 盘绕 (保留); speed 调制跟随刚度与角速度
        private void HoverMovement(Player target, float speed) {
            const float R = 300f, r = 150f, h = 400f, baseW = 0.03f;
            Fig8 += baseW * MathHelper.Clamp(speed, 0.25f, 1.5f);
            if (Fig8 > MathHelper.TwoPi) Fig8 -= MathHelper.TwoPi;
            float ox = R * MathF.Cos(Fig8);
            float oy = r * MathF.Sin(Fig8 * 2f);
            Vector2 desired = target.Center + new Vector2(ox, -h + oy);
            Vector2 toGoal = desired - NPC.Center;
            NPC.velocity = (NPC.velocity * 89f + (toGoal / 8f) * speed) / 90f;
            FaceVelocity();
        }

        private void FaceVelocity() {
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Main.dedServ && ChargeGlow > 0.02f) {
                Texture2D g = ACMAsset.SoftGlow;
                if (g != null) {
                    Color c = Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Holy, WindowVisual) with { A = 0 };
                    float pulse = 1.4f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
                    float scale = pulse * (0.5f + ChargeGlow);
                    spriteBatch.Draw(g, NPC.Center - screenPos, null, c * ChargeGlow, 0f, g.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                }
            }
            return base.PreDraw(spriteBatch, screenPos, drawColor);
        }
    }

    public class ArchosaurBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
            NPC.height = 50;
        }
    }
    public class ArchosaurBody2 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
            if (SummonCount == SummonMax / 3 * 2 || SummonCount == 15)
                SummonNPCType = ModContent.NPCType<ArchosaurBody1>();
            if (SummonCount > SummonMax - 15)
                SummonNPCType = ModContent.NPCType<ArchosaurBody3>();
        }
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
        }
    }
    public class ArchosaurBody3 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody4>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class ArchosaurBody4 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurTail>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class ArchosaurTail : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Tail;
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
}

using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
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

        public enum AttackPhase { Phase1, Phase2 }

        public AttackPhase CurrentPhase => NPC.life > NPC.lifeMax * 0.6f ? AttackPhase.Phase1 : AttackPhase.Phase2;

        // ——雷球——
        private const int ThunderballCD = 180;   // 每 3 秒一次
        public int thunderballTimer = ThunderballCD;

        // ——分身——
        private const int CloneInterval = 60 * 20; // 每 20 s 可召一次
        public int cloneTimer = CloneInterval;

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
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
        }
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            _ = TextureAssets.Npc[Type].Value;
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new(NPC.spriteDirection == -1 ? 0 : tex.Width, 20);
            if (NPCWormType == WormType.Head) // 头部执行AI
            {
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
            new($"{nameof(AncientChineseMythology)}/Sounds/Archosaur/ArchosaurSummon") {
                Volume = 1f,
                PitchVariance = .12f,
                MaxInstances = 5,
            };
        private static readonly SoundStyle DeathSfx =
            new($"{nameof(AncientChineseMythology)}/Sounds/Archosaur/ArchosaurDeath") {
                Volume = 1f,
                PitchVariance = .04f,
                MaxInstances = 3,
            };

        private const string BattleMusicPath = "AncientChineseMythology/Sounds/Archosaur/ArchosaurBattle";  // 不带扩展名

        public override WormType NPCWormType => WormType.Head;
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur_Head";

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
        }

        public override void SetStaticDefaults() {
            // 标记为真正的 Boss —— 决定血条 & 播报
            NPCID.Sets.ShouldBeCountedAsBoss[NPC.type] = true;  // 触发血条、旗帜、BGM 切换等 
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true; // 离屏也绘制（防止头贴丢失）

            // 如果想在图鉴排在 Boss 区域，可提权
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);

            Music = MusicLoader.GetMusicSlot(Mod, BattleMusicPath);

            // 冲突优先级：比普通事件更高、比 Moon Lord 低     :contentReference[oaicite:4]{index=4}
            SceneEffectPriority = SceneEffectPriority.BossHigh;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
        }

        public override void OnSpawn(IEntitySource source) =>
            SoundEngine.PlaySound(SummonSfx, NPC.Center);

        public override void OnKill() =>
            SoundEngine.PlaySound(DeathSfx, NPC.Center);

        public override void AI() {
            // ====== 基础移动 ======
            HoverMovement();

            // ====== 初始化一次性变量 ======
            if (NPC.localAI[3] == 0f) {                // 用 localAI[3] 充当“是否初始化过”
                NPC.ai[3] = -1;                      // 暂无分身
                NPC.ai[0] = 0;                       // 0 = P1, 1 = P2
                NPC.ai[2] = 0;
                NPC.localAI[0] = 180;                  // 雷球 CD
                NPC.localAI[1] = 45;                   // 闪电 CD
                NPC.localAI[2] = 1200;                 // 分身 CD（20s）
                NPC.localAI[3] = 1f;
            }

            // ====== 根据血量切换阶段 ======
            if (NPC.ai[0] == 0 && NPC.life <= NPC.lifeMax * 0.60f) {
                NPC.ai[0] = 1;             // 进入 Phase-2
                if (NPC.ai[3] == -1)       // 没有现存分身
                    SpawnClone();          // 立即召唤
            }

            // ====== 处理分身存在 / 无敌判定 ======
            if (NPC.ai[3] != -1 && Main.npc[(int)NPC.ai[3]].active) {
                NPC.dontTakeDamage = true; // 分身存活，本体无敌
            }
            else {
                NPC.dontTakeDamage = false;
                NPC.ai[3] = -1;            // 保证索引清空
            }

            bool prevCloneAlive = NPC.ai[2] == 1f;
            bool cloneAlive = NPC.ai[3] != -1 && Main.npc[(int)NPC.ai[3]].active;

            // ====== 分身冷却 ======

            if (NPC.ai[0] == 1 && !cloneAlive) {
                if (--NPC.localAI[2] <= 0) {
                    NPC.localAI[2] = cloneTimer;
                    SpawnClone();
                    return;                // 当帧只做召唤
                }
            }

            // ====== 阶段 1 – 雷球 ======
            if (--NPC.localAI[0] <= 0) {
                NPC.localAI[0] = thunderballTimer;   // 3 s
                ThrowThunderballs();
            }
        }

        void ThrowThunderballs() {
            if (!NPC.HasValidTarget) NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            const float baseSpeed = 11f;

            /* ① 本次要发多少颗？——7 ~ 10 随机 */
            int count = Main.rand.Next(7, 11);      // 包含 10

            /* ② 基准方向：指向玩家 */
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            /* ③ 依次生成 */
            for (int i = 0; i < count; i++) {
                /*   a. 让弹道在 ±35° 以内随机偏转，保证散射而不离谱   */
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-35f, 35f));
                Vector2 dir = toPlayer.RotatedBy(angleOffset);

                /*   b. 速度也轻微抖动（90 % ~ 110 %） */
                float speed = baseSpeed * Main.rand.NextFloat(0.9f, 1.1f);

                Projectile.NewProjectileDirect(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    dir * speed,
                    ModContent.ProjectileType<ThunderOrb>(),
                    999, 0f,
                    -1, NPC.whoAmI);   // ai0 = 宿主索引
            }

            /* ④ Phase 1 自残，Phase 2 不再自残 */
            if (NPC.ai[0] == 0)            // 0 = Phase-1
                SelfDamage(0.01f);         // 一次只扣 3 %
        }


        void SpawnClone() {
            Vector2 pos = NPC.Center + Main.rand.NextVector2CircularEdge(250f, 250f);

            int id = NPC.NewNPC(                       // 只填必要参数
                NPC.GetSource_FromAI(),
                (int)pos.X, (int)pos.Y,
                ModContent.NPCType<CloneBossHead>());  // 用真正的头部类

            Main.npc[id].ai[1] = NPC.whoAmI;           // 若要让分身知道宿主 → 存 ai[1]
            NPC.ai[3] = id;                            //    宿主这边继续记录分身索引
        }

        void SelfDamage(float ratio) {
            int dmg = (int)(NPC.lifeMax * ratio);
            NPC.life -= dmg;
            if (NPC.life < 0) NPC.life = 0;
            CombatText.NewText(NPC.Hitbox, Color.OrangeRed, dmg);
        }

        private void HoverMovement() {
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            /* === 8 字轨迹参数 === */
            const float R = 300f;    // 左右半径
            const float r = 150f;    // 上下半径
            const float h = 400f;    // 离玩家头顶高度
            const float ω = 0.03f;   // 每帧角度增量（≈ 9 秒一圈）

            NPC.ai[1] += ω;                       // t ← t + ω
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;    // 保持 0-2π

            /* Lissajous 位置 */
            float offsetX = R * MathF.Cos(NPC.ai[1]);
            float offsetY = r * MathF.Sin(NPC.ai[1] * 2f);

            Vector2 desiredPos = target.Center + new Vector2(offsetX, -h + offsetY);

            /* === 惯性插值到目标点 === */
            Vector2 toGoal = desiredPos - NPC.Center;
            const float inertia = 90f;            // 越大越平滑
            NPC.velocity = (NPC.velocity * (inertia - 1) + toGoal / 8f) / inertia;

            /* === 朝向 & 旋转 === */
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }
    }
    public class ArchosaurBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
        }
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

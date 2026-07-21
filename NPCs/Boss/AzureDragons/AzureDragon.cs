using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙真身 — 蠕虫型多节 Boss 共享基类 (V3 重做)。
    /// 主题: 行云布雨的雷霆之主。青蓝 + 雷电青白 + 雨霁暖金。
    /// 绘制全部由头部在 AzureDragonDraw 中统一承担 (体节 PreDraw 直接跳过),
    /// 体节仅负责跟随运动、轻量粒子与受击结算; 编排/演出状态全在头部。
    /// </summary>
    public abstract class AzureDragon : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/AzureDragons/" + Name;

        /// <summary>不用 BasicWorm 的自动 SpriteDirection 翻转; 朝向在统一绘制里用滞回处理。</summary>
        public override bool IsUseSpriteDirection => false;

        #region 主题色

        /// <summary>苍龙青蓝主色。</summary>
        public static readonly Color DragonCyan = new(40, 200, 255);
        /// <summary>雷电青白副色。</summary>
        public static readonly Color DragonLightning = new(160, 220, 255);
        /// <summary>深蓝底色。</summary>
        public static readonly Color DragonDeep = new(20, 80, 180);
        /// <summary>雨霁暖金 (死亡演出收尾色)。</summary>
        public static readonly Color DawnGold = new(255, 214, 150);

        #endregion

        #region 共享状态

        /// <summary>体节脉动相位 (纯视觉)。</summary>
        protected float segmentPulsePhase;

        /// <summary>贴图垂直翻转滞回 — 只在水平速度明显时改向, 避免体节速度过零时高频抖动。</summary>
        public bool VisualFlip;

        /// <summary>目标玩家。</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>体节所属的头部实例 (头部自身返回 null)。</summary>
        public AzureDragonHead HeadOwner {
            get {
                if (NPC.realLife < 0 || NPC.realLife >= Main.maxNPCs)
                    return null;
                NPC head = Main.npc[NPC.realLife];
                return head.active && head.ModNPC is AzureDragonHead h ? h : null;
            }
        }

        #endregion

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[NPC.type] = 3;
            NPCID.Sets.TrailCacheLength[NPC.type] = 12;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 80;
            NPC.height = 80;
            NPC.lifeMax = 12000000;
            NPC.damage = 300;
            NPC.defense = 120;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 10);
            NPC.netAlways = true;
            SummonMax = 80;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.4f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }
        }

        public override bool CheckActive() => false;

        /// <summary>演出节拍 (入场/换阶段/死亡) 中全身不结算接触伤害 (公平阀门)。</summary>
        public bool CinematicNoContact {
            get {
                AzureDragonHead head = this as AzureDragonHead ?? HeadOwner;
                return head != null && head.State is AzureDragonHead.AIState.Intro
                    or AzureDragonHead.AIState.Transition2
                    or AzureDragonHead.AIState.Transition3
                    or AzureDragonHead.AIState.DeathCinematic;
            }
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return !CinematicNoContact;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.velocity.ToRotation();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (NPCWormType != WormType.Head)
                return false;
            return null;
        }

        public override void AI() {
            base.AI();

            segmentPulsePhase += 0.06f;

            // 翻转滞回
            if (NPC.velocity.X > 1.5f)
                VisualFlip = false;
            else if (NPC.velocity.X < -1.5f)
                VisualFlip = true;

            // 无敌状态跟随头部 (realLife 只对体节有效)
            if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs && Main.npc[NPC.realLife].active)
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;

            float pulse = 0.8f + 0.2f * MathF.Sin(segmentPulsePhase + SummonCount * 0.3f);
            Lighting.AddLight(NPC.Center, DragonCyan.ToVector3() * 0.4f * pulse);

            // 高速运动时的稀疏电尘 (速度门控, 快才闪)
            if (!VaultUtils.isServer && NPC.velocity.LengthSquared() > 400f && Main.rand.NextBool(5)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.4f, NPC.height * 0.4f);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 120, default, 1.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.1f;
            }
        }
    }
}

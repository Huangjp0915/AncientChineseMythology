using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙 - 四圣兽之一，东方苍龙
    /// 超级大后期蠕虫类Boss，千万级血量
    /// 颜色主题：青蓝色 + 电弧雷光
    /// 继承BasicWorm实现蠕虫体节系统
    /// </summary>
    public abstract class AzureDragon : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/AzureDragons/" + Name;

        /// <summary>
        /// 不使用BasicWorm的自动SpriteDirection翻转，我们在PreDraw中手动处理
        /// 这样PostAI不会修改spriteDirection，也不会在rotation上叠加Pi
        /// </summary>
        public override bool IsUseSpriteDirection => false;

        #region 常量

        /// <summary>青龙的青蓝主色</summary>
        public static readonly Color DragonCyan = new(40, 200, 255);
        /// <summary>青龙的雷电副色</summary>
        public static readonly Color DragonLightning = new(160, 220, 255);
        /// <summary>青龙的深蓝底色</summary>
        public static readonly Color DragonDeep = new(20, 80, 180);

        #endregion

        #region 共享属性

        /// <summary>体节脉动相位</summary>
        protected float segmentPulsePhase;
        /// <summary>体节发光强度</summary>
        protected float segmentGlowIntensity = 1f;

        /// <summary>目标玩家</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
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

            segmentPulsePhase += 0.06f + SummonCount * 0.002f;

            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;

            // 青蓝色光照
            float pulse = 0.8f + 0.2f * MathF.Sin(segmentPulsePhase);
            Lighting.AddLight(NPC.Center, DragonCyan.ToVector3() * 0.5f * pulse * segmentGlowIntensity);

            // 体节运动粒子
            if (!VaultUtils.isServer && NPC.velocity.LengthSquared() > 4f && Main.rand.NextBool(3)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.4f, NPC.height * 0.4f);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.15f;
            }
        }

        /// <summary>
        /// 绘制体节的青蓝光效叠加
        /// </summary>
        protected void DrawSegmentGlow(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (ACMAsset.SoftGlow == null) return;

            float pulse = 0.6f + 0.4f * MathF.Sin(segmentPulsePhase);
            Color glowColor = DragonCyan * (0.3f * pulse * segmentGlowIntensity);
            glowColor.A = 0;

            spriteBatch.Draw(
                ACMAsset.SoftGlow,
                NPC.Center - screenPos,
                null,
                glowColor,
                0f,
                new Vector2(ACMAsset.SoftGlow.Width / 2f, ACMAsset.SoftGlow.Height / 2f),
                1.5f + 0.3f * pulse,
                SpriteEffects.None,
                0f
            );
        }
    }
}

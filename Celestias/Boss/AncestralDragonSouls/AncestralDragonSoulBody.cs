using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂身体段 — 逻辑节点: 位置/受伤/光照。
    /// 绘制全部由宿主龙头合批完成 (星尘着色器逐节参数), 本类不再自绘。
    /// </summary>
    public class AncestralDragonSoulBody : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Body;

        private float localPulseOffset;

        public override void ChangeSummonType() {
            // 根据召唤计数决定生成身体还是尾巴
            if (SummonCount >= SummonMax - 1) {
                SummonNPCType = ModContent.NPCType<AncestralDragonSoulTail>();
            }
            else {
                SummonNPCType = ModContent.NPCType<AncestralDragonSoulBody>();
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 70;
            NPC.height = 70;
            NPC.lifeMax = 9500000;
            NPC.damage = 280;
            NPC.defense = 100;
        }

        /// <summary>体节限伤：身体段受到的伤害降低85%</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 0.15f;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);

            segmentIndex = SummonCount;
            localPulseOffset = segmentIndex * 0.3f;
        }

        public override void AI() {
            base.AI();

            // 身体段特有的波动效果
            soulPulsePhase = globalTime * 2f + localPulseOffset;

            // 根据位置变化光照强度 (基类已按虚化/星散衰减主光, 此处叠加位置渐变)
            float pulseIntensity = 0.5f + MathF.Sin(soulPulsePhase) * 0.15f;
            float fadeRatio = (float)segmentIndex / 80f;
            float lightIntensity = (0.8f - fadeRatio * 0.3f) * pulseIntensity * (1f - GhostLevel * 0.5f) * (1f - deathDissolve);

            Lighting.AddLight(NPC.Center, new Vector3(0.85f, 0.9f, 1f) * lightIntensity);
        }

        public override void OnKill() {
            // 身体段死亡粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

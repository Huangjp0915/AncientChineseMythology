using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂尾部 — 尾梢逻辑节点: 摆动 + 受门控的尾扫攻击 (有汇聚预警, 过场/喘息静默)。
    /// 绘制由宿主龙头合批完成 (含尾尖光点)。
    /// </summary>
    public class AncestralDragonSoulTail : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Tail;

        internal override float DrawRotationOffset => MathHelper.PiOver2;

        private float tailSwayPhase;
        private int attackCooldown = 200;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.lifeMax = 9500000;
            NPC.damage = 300;
            NPC.defense = 80;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);
            segmentIndex = SummonCount;
        }

        /// <summary>体节限伤：尾部受到的伤害降低75%</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 0.25f;
        }

        public override void AI() {
            base.AI();

            tailSwayPhase += 0.1f;
            soulPulsePhase = globalTime * 2.5f;

            // 尾巴摆动效果
            float swayAmount = MathF.Sin(tailSwayPhase) * 0.15f;
            if (FatherNPC != null && FatherNPC.active) {
                Vector2 perpendicular = NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                NPC.position += perpendicular * swayAmount * 3f;
            }

            // 尾扫攻击: 只在宿主头的作战节拍开火 (过场/喘息/谜题布场/死亡静默)
            AncestralDragonSoulHead head = OwnerHead;
            bool combatReady = head != null && head.TailMayAttack;
            if (combatReady) {
                attackCooldown--;

                // 发射前 22f 尾梢汇聚星尘 = 可读预警
                if (attackCooldown < 22 && Main.netMode != NetmodeID.Server && attackCooldown % 2 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * Main.rand.NextFloat(40f, 110f);
                    int dust = Dust.NewDust(pos, 0, 0, DustID.Clentaminator_Cyan, 0, 0, 140, Color.White, 1.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - pos) * 0.1f;
                }

                if (attackCooldown <= 0) {
                    attackCooldown = Main.expertMode ? 130 : 160;
                    PerformTailAttack();
                }
            }

            // 尾部发光更强 (随虚化/星散衰减)
            float pulseIntensity = (0.7f + MathF.Sin(soulPulsePhase) * 0.25f) * (1f - GhostLevel * 0.5f) * (1f - deathDissolve);
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.95f, 1f) * pulseIntensity);
        }

        private void PerformTailAttack() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 获取头部引用来找到目标
            NPC headNPC = null;
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                headNPC = Main.npc[NPC.realLife];
            }

            if (headNPC == null) return;

            Player target = Main.player[headNPC.target];
            if (!target.active || target.dead) return;

            // 发射龙尾扫击波 (出膛热身由弹幕自理, 防 telefrag)
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = -2; i <= 2; i++) {
                float angle = MathHelper.ToRadians(i * 20);
                Vector2 vel = toPlayer.RotatedBy(angle) * 10f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    vel,
                    ModContent.ProjectileType<TailSweepWave>(),
                    NPC.damage / 3,
                    2f
                );
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);

            // 扫击粒子
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Cloud, vel.X, vel.Y, 180, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill() {
            // 尾部死亡粒子爆发
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 7);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Frost
                };
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f }, NPC.Center);
        }
    }
}

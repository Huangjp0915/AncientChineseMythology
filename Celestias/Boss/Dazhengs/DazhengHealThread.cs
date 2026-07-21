using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「生命汲取·治疗线」— 冬季签名 (取代隐形收缩弹幕)。
    ///
    /// 一条从大椿连向目标玩家的<b>可见治疗导管</b>: 蓄能(安全色)后转为汲取(鬼绿拉拽)。
    /// 导管期间大椿持续回血; 玩家<b>冲刺或跳跃</b>即可挣断 (打断 + 开破绽窗口)。
    /// 把"看不见的收缩牢笼"变成"看得见、可被主动打断的资源博弈"。
    ///
    /// V3「斩线」演出: ai[2]=1 时进入 20t 回抽鞭甩 — 断点从玩家端弹性抽回大椿,
    /// 断头甩尾 + 沿线爆出断裂尘, 给玩家最帅的主动反制一个配得上的画面。
    ///
    /// 纯视觉载体: ai[0]=大椿 whoAmI, ai[1]=目标玩家索引; 汲取/挣断判定由 <see cref="Dazheng"/> 权威执行。
    /// 通过 <see cref="Dazheng.HealThreadActive"/>(蓄能/汲取阶段) 切换视觉。绘制 client-only。
    /// </summary>
    public class DazhengHealThread : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int SnapTicks = 20; // 斩线回抽动画时长

        private int BossIndex => (int)Projectile.ai[0];
        private int TargetIndex => (int)Projectile.ai[1];
        /// <summary>斩线模式 (由大椿在挣断瞬间设 1; 动画放完自灭)。</summary>
        private bool Snapping => Projectile.ai[2] == 1f;

        private float anim;
        private int snapTimer;
        private Vector2 snapAnchor; // 挣断瞬间的玩家端断点 (回抽起点)

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 12; // 由大椿每帧续命
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            anim += 0.12f;

            if (BossIndex < 0 || BossIndex >= Main.maxNPCs ||
                !Main.npc[BossIndex].active || Main.npc[BossIndex].type != ModContent.NPCType<Dazheng>()) {
                Projectile.Kill();
                return;
            }

            NPC boss = Main.npc[BossIndex];
            Projectile.Center = boss.Center;

            // —— 斩线回抽 (纯视觉, 20t 自灭; 由本体续命保证放完) ——
            if (Snapping) {
                if (snapTimer == 0) {
                    // 锁定断点 + 沿线断裂爆尘 (一次性)
                    if (TargetIndex >= 0 && TargetIndex < Main.maxPlayers)
                        snapAnchor = Main.player[TargetIndex].Center;
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 26; i++) {
                            Vector2 along = Vector2.Lerp(boss.Center, snapAnchor, Main.rand.NextFloat());
                            Dust d = Dust.NewDustPerfect(along, DustID.GreenFairy,
                                Main.rand.NextVector2Circular(4f, 4f), 90, default, Main.rand.NextFloat(1.2f, 2f));
                            d.noGravity = true;
                        }
                    }
                }
                snapTimer++;
                Projectile.timeLeft = 6; // 不再受大椿续命依赖, 自走完动画
                if (snapTimer > SnapTicks)
                    Projectile.Kill();
                return;
            }

            if (TargetIndex >= 0 && TargetIndex < Main.maxPlayers) {
                Player t = Main.player[TargetIndex];
                if (Main.netMode != NetmodeID.Server && t.active && !t.dead && Main.rand.NextBool(2)) {
                    Vector2 along = Vector2.Lerp(boss.Center, t.Center, Main.rand.NextFloat());
                    Dust d = Dust.NewDustPerfect(along + Main.rand.NextVector2Circular(14, 14),
                        DustID.GreenFairy, Vector2.Zero, 120, default, 1.1f);
                    d.noGravity = true;
                    // 汲取期粒子向 Boss 回流
                    d.velocity = (boss.Center - along).SafeNormalize(Vector2.Zero) * 3f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            if (BossIndex < 0 || BossIndex >= Main.maxNPCs)
                return false;
            NPC boss = Main.npc[BossIndex];

            // —— 斩线回抽鞭甩: 断头从玩家端弹性抽回, 尾端横甩衰减 ——
            if (Snapping) {
                if (snapTimer == 0)
                    return false; // 断点尚未锁定 (等待首帧 AI)
                float p = MathHelper.Clamp(snapTimer / (float)SnapTicks, 0f, 1f);
                // 弹性回缩: 立即加速离开断点, 尾段渐止 (1-(1-p)^3)
                float retract = 1f - MathF.Pow(1f - p, 3f);
                Vector2 freeEnd = Vector2.Lerp(snapAnchor, boss.Center, retract);
                // 断头横甩: 垂直方向衰减摆动 (鞭子的余势)
                Vector2 dir = (snapAnchor - boss.Center).SafeNormalize(Vector2.UnitX);
                Vector2 perp = new(-dir.Y, dir.X);
                freeEnd += perp * MathF.Sin(p * 14f) * 60f * (1f - p);

                float fade = 1f - p;
                ACMShaders.DrawBeam(freeEnd, boss.Center, 7f * fade + 2f,
                    new Color(190, 255, 200), TelegraphColors.GhostGreen, 0.85f * fade,
                    flowSpeed: 4f, flowScale: 2.4f, coreSharp: 2.0f);
                return false;
            }

            if (TargetIndex < 0 || TargetIndex >= Main.maxPlayers)
                return false;
            Player t = Main.player[TargetIndex];
            if (!t.active || t.dead)
                return false;

            bool draining = Dazheng.HealThreadActive;
            float pulse = 0.6f + 0.4f * MathF.Sin(anim * 3f);

            // 蓄能=玉青(安全提示), 汲取=鬼绿(拉拽危机); 红色只留给真正伤害源, 故此处不用红
            Color core = draining ? new Color(150, 255, 170) : TelegraphColors.Safe;
            Color edge = draining ? TelegraphColors.GhostGreen : new Color(180, 255, 200);
            float halfWidth = (draining ? 10f : 5f) * pulse;
            float intensity = draining ? 1f : 0.55f;

            ACMShaders.DrawBeam(t.Center, boss.Center, halfWidth, core, edge, intensity,
                flowSpeed: draining ? 2.6f : 1.2f, flowScale: 2.4f, coreSharp: 2.0f);

            return false;
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥狐火魂弹 (Nether Foxfire Soul) —— 自定义青蓝魂火。
    ///
    /// 三种形态由 <see cref="Variant"/> 区分 (《虚实九影》真假博弈):
    ///   0 = 实·狐火 (青蓝芯, 实体伤害, 命中叠魂蚀)；
    ///   1 = 虚·幻影 (幽紫半透, damage=0 → 无害, 仅作真假误导)；
    ///   2 = 真身·裁决 (柔白芯, 实体伤害 + 命中叠冥律, P3 真身专用)。
    ///
    /// V3: 焰体升级为 NetherKitsuneSoulflame 程序化撕裂鬼火 (经 FogSystem 累积通道一批绘制),
    /// 淡入期无伤害 (伤害窗=视觉窗), 雾转鬼绿时焰色跟随 (MistGhost, 纯客户端视觉)。
    /// </summary>
    public class NetherFoxfireSoul : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/NetherKitsunes/NetherMissesTop";

        /// <summary>0=实狐火 1=虚幻影 2=真身裁决。</summary>
        public ref float Variant => ref Projectile.ai[0];
        private ref float WavePhase => ref Projectile.ai[1];

        private bool IsIllusion => (int)Variant == 1;
        private bool IsJudgment => (int)Variant == 2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.alpha = 255;          // 出场渐显 (telegraph)
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            // 出场渐显: 命中前 ~0.4s 由淡转实, 给可读预警窗口
            if (Projectile.alpha > 0)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 22);

            // 狐火飘忽: 沿速度法线轻微正弦摆动 (鬼火质感, 不改变整体航向)
            WavePhase += 0.22f;
            float speed = Projectile.velocity.Length();
            if (speed > 0.01f) {
                Vector2 fwd = Projectile.velocity / speed;
                Vector2 perp = new Vector2(-fwd.Y, fwd.X);
                Projectile.position += perp * MathF.Sin(WavePhase) * 1.1f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            Color c = SoulColor();
            if (!Main.dedServ && Main.rand.NextBool(IsIllusion ? 3 : 2)) {
                Vector2 dpos = Projectile.Center + Main.rand.NextVector2Circular(6f, 6f);
                int d = Dust.NewDust(dpos, 1, 1, IsJudgment ? DustID.WhiteTorch : DustID.BlueTorch, 0, 0,
                    IsIllusion ? 170 : 120, c, IsIllusion ? 1.1f : 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.8f, 0.8f);
            }

            // 焰舌: 提交到 FogSystem 魂焰累积通道 (一批绘制, 焰尖拖在速度反方向)
            if (!Main.dedServ && Projectile.alpha < 200) {
                float vis = 1f - Projectile.alpha / 255f;
                float ghost = IsIllusion ? 0f : NetherKitsuneFogSystem.MistGhost;
                NetherKitsuneFogSystem.RequestGroundFlame(new NetherKitsuneFogSystem.SoulflameSpec {
                    WorldPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 6f,
                    WidthPx = IsIllusion ? 30f : 40f,
                    HeightPx = (IsIllusion ? 52f : 74f) * (0.8f + 0.2f * MathF.Sin(WavePhase * 1.3f)),
                    Intensity = vis * (IsIllusion ? 0.55f : 0.9f),
                    Ghost = ghost,
                    Rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2,
                    Seed = Projectile.whoAmI * 0.37f,
                    Core = IsJudgment ? new Color(255, 250, 240) : SoulColor(),
                    Edge = IsIllusion ? new Color(90, 60, 160) : new Color(60, 110, 200),
                });
            }

            Lighting.AddLight(Projectile.Center, c.ToVector3() * (IsIllusion ? 0.25f : 0.45f));
        }

        // 淡入期无伤害 — 伤害窗口与视觉严格对齐 (公平阀门)
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.alpha > 90)
                return false;
            return null;
        }

        private Color SoulColor() => (int)Variant switch {
            1 => new Color(160, 120, 230),  // 虚·幻影 幽紫
            2 => new Color(235, 245, 255),  // 真身·裁决 柔白
            _ => new Color(130, 210, 255),  // 实·狐火 青蓝
        };

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 地府身份层: 实体狐火命中叠魂蚀; 真身裁决额外叠冥律 (轻度接入)
            UnderworldField.AddSoulErosion(target, IsJudgment ? 2 : 1);
            if (IsJudgment)
                UnderworldField.AddNetherDecree(target, 1);
        }

        public override void OnKill(int timeLeft) {
            // 吐息狐火 (variant 3): 熄灭处留怨火地灾 (仅服务端/单机, 带同屏上限)
            if ((int)Variant == 3 && Main.netMode != NetmodeID.MultiplayerClient) {
                int patchType = ModContent.ProjectileType<NetherGhostflamePatch>();
                int count = 0;
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == patchType)
                        count++;
                }
                if (count < 10 && Main.rand.NextBool(2))
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, patchType, 0, 0f, Main.myPlayer);
            }

            if (Main.dedServ)
                return;
            Color c = SoulColor();
            for (int i = 0; i < (IsIllusion ? 5 : 9); i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                int d = Dust.NewDust(Projectile.Center, 1, 1, IsJudgment ? DustID.WhiteTorch : DustID.BlueTorch,
                    vel.X, vel.Y, 100, c, 1.5f);
                Main.dust[d].noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;

            Vector2 origin = glow.Size() * 0.5f;
            float vis = 1f - Projectile.alpha / 255f;
            float pulse = 1f + 0.15f * MathF.Sin(WavePhase * 1.7f);
            Color soul = SoulColor();
            Color outer = IsIllusion ? new Color(90, 60, 160) : new Color(80, 120, 200);

            // 魂火拖尾 (取历史点, 加性柔光叠层)
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 op = Projectile.oldPos[i];
                if (op == Vector2.Zero)
                    continue;
                float t = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 dp = op + Projectile.Size * 0.5f - Main.screenPosition;
                Color tc = Color.Lerp(outer, soul, t) * (vis * t * (IsIllusion ? 0.28f : 0.45f));
                tc.A = 0;
                float ts = (Projectile.width / (float)glow.Width) * (0.5f + t * 0.5f) * (IsIllusion ? 1.1f : 1.4f);
                sb.Draw(glow, dp, null, tc, 0f, origin, ts, SpriteEffects.None, 0f);
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            float coreScale = Projectile.width / (float)glow.Width;

            // 外晕
            Color halo = outer * (vis * 0.5f); halo.A = 0;
            sb.Draw(glow, center, null, halo, 0f, origin, coreScale * 2.4f * pulse, SpriteEffects.None, 0f);
            // 主魂火
            Color mid = soul * (vis * (IsIllusion ? 0.55f : 0.85f)); mid.A = 0;
            sb.Draw(glow, center, null, mid, 0f, origin, coreScale * 1.5f * pulse, SpriteEffects.None, 0f);
            // 高亮芯
            Color core = Color.Lerp(soul, Color.White, IsJudgment ? 0.7f : 0.45f) * vis; core.A = 0;
            sb.Draw(glow, center, null, core, 0f, origin, coreScale * 0.8f, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }

    /// <summary>
    /// 尾击判定线 (Nether Tail Strike) —— 尾巴刺击的服务器权威伤害载体 (不可见, 视觉由尾巴本体承担)。
    /// ai[0]=角度, ai[1]=线长, ai[2]=起爆延迟帧; 伤害窗仅 [延迟, 延迟+7f) —— 与尾巴 poly12 爆发段严格对齐。
    /// 琶音刺 / 钳击合刺 / 虚空九刺 / 幻影下砸共用。
    /// </summary>
    public class NetherTailStrike : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int ActiveTime = 7;

        private float StrikeAngle => Projectile.ai[0];
        private float StrikeLength => Projectile.ai[1];
        private int Delay => (int)Projectile.ai[2];

        // 用 localAI 计龄 (timeLeft 不随生成包同步, 远端会拿到默认值; localAI 各端自增, 偏差仅 1~2f)
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120; // 兜底; 实际寿命由 Age 驱动
            Projectile.alpha = 255;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Age++;
            if (Age > Delay + ActiveTime + 2)
                Projectile.Kill();
        }

        // 伤害窗与尾巴爆发段严格对齐 (公平阀门)
        public override bool? CanDamage() {
            return Age >= Delay && Age < Delay + ActiveTime ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 end = Projectile.Center + StrikeAngle.ToRotationVector2() * StrikeLength;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, end, 30f, ref _);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) => false; // 视觉由尾巴承担
    }

    /// <summary>
    /// 怨火地灾 (Nether Ghostflame Patch) —— 狐火落点残留的鬼绿 DoT 场。
    /// 无直接接触伤害 (damage=0 生成), 周期性给场内玩家叠魂蚀 (§6.1 地府 DoT=鬼绿, 非致命色);
    /// zoning 用: 逼玩家持续走位, 与雾隐扑袭构成空间压力。视觉=三舌魂焰 + 鬼绿柔光, 无新贴图。
    /// </summary>
    public class NetherGhostflamePatch : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int FadeInTime = 20;
        private const int FadeOutTime = 40;

        private int stackTimer;

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 70;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.alpha = 255;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.velocity *= 0.85f;

            // 淡入淡出
            float fade = FadeFactor();
            Projectile.alpha = (int)(255f * (1f - fade));

            // 周期性给场内玩家叠魂蚀 (无直接伤害 — DoT 场身份)
            stackTimer++;
            if (stackTimer >= 20) {
                stackTimer = 0;
                float r = Projectile.width * 0.72f;
                foreach (var p in Main.ActivePlayers) {
                    if (!p.dead && p.Distance(Projectile.Center) < r)
                        UnderworldField.AddSoulErosion(p, 1);
                }
            }

            if (!Main.dedServ) {
                // 三舌魂焰 (鬼绿, 相位错开)
                float baseSeed = Projectile.whoAmI * 0.53f;
                for (int i = 0; i < 3; i++) {
                    float off = (i - 1) * Projectile.width * 0.30f;
                    float sway = MathF.Sin((float)Main.timeForVisualEffects * 0.05f + baseSeed + i * 2.1f) * 0.12f;
                    NetherKitsuneFogSystem.RequestGroundFlame(new NetherKitsuneFogSystem.SoulflameSpec {
                        WorldPos = Projectile.Center + new Vector2(off, Projectile.height * 0.4f),
                        WidthPx = 46f - MathF.Abs(i - 1) * 10f,
                        HeightPx = (86f - MathF.Abs(i - 1) * 22f) * fade,
                        Intensity = fade * 0.85f,
                        Ghost = 1f, // 怨火恒为鬼绿 (DoT 契约色)
                        Rotation = sway,
                        Seed = baseSeed + i * 1.7f,
                        Core = new Color(180, 255, 210),
                        Edge = new Color(40, 140, 80),
                    });
                }

                if (Main.rand.NextBool(5)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.4f, 20f),
                        DustID.CursedTorch, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.8f)), 130,
                        TelegraphColors.GhostGreen, 1.2f * fade);
                    d.noGravity = true;
                }

                Lighting.AddLight(Projectile.Center, TelegraphColors.GhostGreen.ToVector3() * 0.4f * fade);
            }
        }

        private float FadeFactor() {
            int age = 200 - Projectile.timeLeft;
            if (age < FadeInTime)
                return age / (float)FadeInTime;
            if (Projectile.timeLeft < FadeOutTime)
                return Projectile.timeLeft / (float)FadeOutTime;
            return 1f;
        }

        // 不造成直接接触伤害 — 威胁全部由魂蚀 DoT 表达 (视觉=鬼绿, 遵守"红=致命"契约)
        public override bool CanHitPlayer(Player target) => false;

        // 视觉全部由 Soulflame 累积通道承担
        public override bool PreDraw(ref Color lightColor) => false;
    }
}

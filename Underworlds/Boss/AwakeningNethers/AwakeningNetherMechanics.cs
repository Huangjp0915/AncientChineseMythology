using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 虚空魂雾 — 由体节渗出、或吐息扫过的地面残留区域。
    /// 站在其中持续叠加 魂蚀 (Soul Erosion)，是觉醒冥龙的签名地府 DoT 机制。
    /// 本身只造成极低接触伤害，威胁来自魂蚀叠层。
    /// V3: 视觉走 Soulflame 专属着色器 (径向魂雾场形态, 批量队列, 常数批次开销)。
    /// </summary>
    public class AwakeningNetherMiasma : ModProjectile
    {
        // 复用原版暗影球贴图，自绘魂雾，避免缺失 PNG 崩溃。
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowOrb;

        // ai[0] = 体型系数（1 = 标准，>1 更大）
        private float SizeFactor => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];

        private float fade;
        private float swirl;
        private int stackTimer;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            Vector2 c = Projectile.Center;
            int s = (int)(120 * SizeFactor);
            Projectile.width = s;
            Projectile.height = s;
            Projectile.Center = c;
        }

        public override void AI() {
            swirl += 0.05f;

            // 淡入淡出
            float life = Projectile.timeLeft;
            if (life > 420f)
                fade = MathHelper.Lerp(fade, 1f, 0.1f);
            else if (life < 90f)
                fade = life / 90f;
            else
                fade = MathHelper.Lerp(fade, 1f, 0.05f);

            Projectile.velocity *= 0.9f;

            // 周期性给范围内玩家叠加魂蚀
            stackTimer++;
            if (stackTimer >= 18) {
                stackTimer = 0;
                float r = Projectile.width * 0.5f;
                foreach (var p in Main.player) {
                    if (p == null || !p.active || p.dead)
                        continue;
                    if (p.Distance(Projectile.Center) < r)
                        p.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(1);
                }
            }

            // 魂雾场 decal: 鬼绿芯 + 紫缘旋涌 (§6.1 地府 DoT=鬼绿, 单张即含双色阶)
            if (!Main.dedServ) {
                float size = Projectile.width * 2.1f;
                AwakeningNetherScreenSystem.RequestSoulflame(Projectile.Center,
                    swirl.ToRotationVector2(), size, 0.6f * fade,
                    Projectile.whoAmI * 0.31f, 1f,
                    TelegraphColors.GhostGreen, AwakeningNetherHelper.VoidDarkPurple);

                if (Main.rand.NextBool(4)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(Projectile.width * 0.45f);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.CursedTorch : DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.1f * fade;
                    d.velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.6f;
                    d.alpha = 120;
                }
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.VoidDarkPurple.ToVector3() * 0.5f * fade);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(2);
        }

        // 视觉全部由 Soulflame decal 队列承担
        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 魂火余烬 (V3 新增) — 脊波尾鞭 / 衔尾困杀的慢速压场弹。
    /// 低速漂移 + 轻微摇曳, 纯走位可解; 出膛 18f 淡入期无伤害 (公平阀门)。
    /// </summary>
    public class AwakeningNetherSoulWisp : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private const int FadeInTime = 18;

        private float pulsePhase;
        private int age;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 255;
        }

        public override void AI() {
            age++;
            pulsePhase += 0.13f;

            // 淡入
            Projectile.alpha = (int)MathHelper.Lerp(255f, 0f, MathHelper.Clamp(age / (float)FadeInTime, 0f, 1f));

            // 轻微摇曳漂移 (慢即公平, 密度受 Head 端上限约束)
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perp * MathF.Sin(pulsePhase * 0.7f + Projectile.whoAmI) * 0.9f;
            Projectile.velocity *= 0.999f;

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.CursedTorch);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.15f;
                d.alpha = 100;
            }

            Lighting.AddLight(Projectile.Center, TelegraphColors.GhostGreen.ToVector3() * 0.35f);
        }

        // 淡入期无伤害 — 伤害窗口与视觉对齐
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (age < FadeInTime)
                return false;
            return null;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = glow.Size() / 2f;
            float a = 1f - Projectile.alpha / 255f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.18f;

            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                AwakeningNetherHelper.VoidDarkPurple with { A = 0 } * (0.6f * a),
                0f, origin, 1.15f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                TelegraphColors.GhostGreen with { A = 0 } * (0.8f * a),
                0f, origin, 0.66f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * (0.4f * a),
                0f, origin, 0.28f * pulse, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = Main.rand.NextVector2Circular(3.5f, 3.5f);
            }
        }
    }

    /// <summary>
    /// 幽冥体节激光 — 觉醒终末的体节同步激光帘幕。
    /// 前 40 帧为细线预告 (无伤)，之后 40 帧为粗激光 (实伤)。
    /// 让被动跟随的体节成为真正的机制威胁。
    /// </summary>
    public class AwakeningNetherSegmentLaser : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowOrb;

        public const int TelegraphTime = 40;
        public const int ActiveTime = 40;
        public const float BeamLength = 900f;

        // ai[0] = 锚定体节 whoAmI；ai[1] = 激光方向角
        private int AnchorWho => (int)Projectile.ai[0];
        private float Angle => Projectile.ai[1];

        private float Timer => 80 - Projectile.timeLeft;
        private bool IsActive => Timer >= TelegraphTime;
        private Vector2 Dir => Angle.ToRotationVector2();

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphTime + ActiveTime;
            Projectile.alpha = 255;
        }

        public override void AI() {
            // 跟随锚定体节
            if (AnchorWho >= 0 && AnchorWho < Main.maxNPCs) {
                NPC anchor = Main.npc[AnchorWho];
                if (anchor.active)
                    Projectile.Center = anchor.Center;
                else if (IsActive)
                    Projectile.Kill();
            }

            if (Timer == TelegraphTime - 1)
                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);

            if (IsActive && !Main.dedServ) {
                Vector2 step = Dir;
                for (int i = 0; i < 14; i++) {
                    if (!Main.rand.NextBool(3))
                        continue;
                    Vector2 pos = Projectile.Center + step * (i * 64f + Main.rand.NextFloat(64f));
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = step.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.5f, 1.5f);
                }
                Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.6f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!IsActive)
                return false;
            float collisionPoint = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Dir * BeamLength;
            float width = 26f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, width, ref collisionPoint);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Dir * BeamLength;

            if (!IsActive) {
                // 细线预告 — 幽蓝紫(非致命色)渐强 (§6.1 红只留给实伤)
                float t = Timer / (float)TelegraphTime;
                Color edge = Color.Lerp(TelegraphColors.NetherViolet, AwakeningNetherHelper.AwakeningPurple, t);
                ACMShaders.DrawBeam(start, end, 2f + t * 4f, Color.Lerp(edge, Color.White, 0.35f), edge,
                    0.25f + 0.45f * t, flowSpeed: 2.0f, flowScale: 2.4f);
            }
            else {
                // 实体激光帘幕 — 致命(红芯紫边), BeamGrad 流动 + 过曝芯
                float at = (Timer - TelegraphTime) / (float)ActiveTime;
                float widthMod = MathF.Sin(at * MathF.PI) * 0.7f + 0.4f;
                ACMShaders.DrawBeam(start, end, 26f * widthMod, TelegraphColors.Lethal, AwakeningNetherHelper.AwakeningPurple,
                    1f, flowSpeed: 2.6f, flowScale: 2.0f, coreGlow: 1.4f);
                ACMShaders.DrawBeam(start, end, 10f * widthMod, Color.White, TelegraphColors.Lethal,
                    1f, flowSpeed: 2.6f, flowScale: 2.0f, coreGlow: 1.8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 噬魂卫星 — 第三幕「虚空吞噬」中环绕玩家的可清除灵魂球。
    /// 环绕并向内收缩；同步闪光预告后一起朝玩家冲刺 (集中爆发，而非持续喷射)。
    /// 玩家可用武器击碎清除，或走位躲避冲刺。
    /// </summary>
    public class AwakeningNetherSoulSatellite : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowOrb;

        public const int OrbitTime = 230;
        public const int FlashTime = 45;

        // ai[0] = 目标玩家索引；ai[1] = 初始角度偏移
        private int TargetWho => (int)Projectile.ai[0];
        private float StartAngle => Projectile.ai[1];

        private float timer;
        private int clearHits;
        private float angle;
        private float pulse;
        private Vector2 launchDir;
        private bool launched;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OrbitTime + FlashTime + 90;
            Projectile.alpha = 80;
        }

        public override void AI() {
            timer++;
            pulse += 0.15f;
            angle = StartAngle + timer * 0.04f;

            Player target = (TargetWho >= 0 && TargetWho < Main.maxPlayers) ? Main.player[TargetWho] : null;
            if (target == null || !target.active || target.dead) {
                Projectile.velocity *= 0.96f;
            }
            else if (timer < OrbitTime) {
                // 环绕并收缩
                float radius = MathHelper.Lerp(330f, 130f, timer / OrbitTime);
                Vector2 desired = target.Center + angle.ToRotationVector2() * radius;
                Projectile.Center = Vector2.Lerp(Projectile.Center, desired, 0.15f);
                Projectile.velocity = Vector2.Zero;
            }
            else if (timer < OrbitTime + FlashTime) {
                // 闪光预告，锁定方向
                Projectile.velocity *= 0.85f;
                launchDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                if (timer == OrbitTime + 1)
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.3f }, Projectile.Center);
            }
            else if (!launched) {
                launched = true;
                Projectile.velocity = launchDir * 19f;
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, Projectile.Center);
            }

            // 可被友方弹幕清除
            TryGetCleared();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.SoulPink.ToVector3() * 0.5f);
        }

        private void TryGetCleared() {
            foreach (var proj in Main.projectile) {
                if (proj == null || !proj.active || !proj.friendly || proj.hostile || proj.damage <= 0)
                    continue;
                if (proj.Hitbox.Intersects(Projectile.Hitbox)) {
                    clearHits++;
                    if (clearHits >= 3) {
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(1);
        }

        // 形成期：由"魂"溶凝为实体 (soul-dissolve), 第三幕虚空吞噬的可信生成
        private const int FormTime = 28;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;

            // —— 形成期: DissolveBurn 由 1→0 重凝 (魂→实体), 复用 BAW 首发助手 ——
            if (timer < FormTime) {
                Texture2D body = AwakeningNetherHelper.VoidCoreTexture;
                if (body != null && body.Width > 0) {
                    float threshold = 1f - timer / (float)FormTime;
                    float formScale = 32f / body.Width;
                    bool drew = BAWImpermanences.BAWFX.DrawDissolveSprite(sb, body,
                        Projectile.Center - Main.screenPosition, null, AwakeningNetherHelper.SoulPink,
                        pulse, body.Size() / 2f, formScale, SpriteEffects.None,
                        threshold, AwakeningNetherHelper.SoulPink);
                    if (drew)
                        return false; // 溶凝期间只画消融体, 完成后转常规绘制
                }
            }

            bool flashing = timer >= OrbitTime && timer < OrbitTime + FlashTime;
            float scale = 1.1f + MathF.Sin(pulse) * 0.15f;
            Color color = AwakeningNetherHelper.SoulPink;
            if (flashing) {
                float f = MathF.Sin((timer - OrbitTime) / FlashTime * MathF.PI * 6f) * 0.5f + 0.5f;
                color = Color.Lerp(AwakeningNetherHelper.SoulPink, Color.White, f);
                scale += f * 0.4f;
            }

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color tc = color * progress * 0.4f;
                tc.A = 0;
                AwakeningNetherHelper.DrawVoidCore(sb, Projectile.oldPos[i] + Projectile.Size / 2f, tc,
                    Color.Lerp(tc, Color.White, 0.3f), scale * progress * 0.7f, pulse);
            }

            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center, color,
                Color.Lerp(color, Color.White, 0.4f), scale, pulse, flashing);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 虚空魂雾 — 由体节释放、或吐息扫过的地面残留区域。
    /// 站在其中持续叠加 魂蚀 (Soul Erosion)，是觉醒冥龙的签名地府 DoT 机制。
    /// 本身只造成极低接触伤害，威胁来自魂蚀叠层。
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

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(Projectile.width * 0.45f);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.1f * fade;
                d.velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.6f;
                d.alpha = 120;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.VoidDarkPurple.ToVector3() * 0.5f * fade);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(2);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() / 2f;
            float baseScale = Projectile.width / (float)tex.Width;

            // 多层旋转的魂雾团
            int blobs = 10;
            for (int i = 0; i < blobs; i++) {
                float ang = swirl + MathHelper.TwoPi * i / blobs;
                float dist = Projectile.width * 0.3f * (0.5f + 0.5f * MathF.Sin(swirl * 1.3f + i));
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist - Main.screenPosition;
                Color c = Color.Lerp(AwakeningNetherHelper.VoidDarkPurple, AwakeningNetherHelper.AwakeningPurple, (float)i / blobs);
                c.A = 0;
                sb.Draw(tex, pos, null, c * 0.35f * fade, ang, origin, baseScale * 1.6f, SpriteEffects.None, 0f);
            }

            // 中心深渊
            Color core = AwakeningNetherHelper.VoidDarkPurple;
            core.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, core * 0.5f * fade, swirl, origin, baseScale * 2.2f, SpriteEffects.None, 0f);

            // 鬼绿内核 — 标示这是魂蚀 DoT 场(非致命, §6.1 地府 DoT=鬼绿), 与"红=致命"区分
            Color dot = TelegraphColors.GhostGreen;
            dot.A = 0;
            float dotPulse = 0.18f + 0.10f * MathF.Sin(swirl * 1.7f);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, dot * dotPulse * fade, -swirl * 0.6f, origin, baseScale * 1.3f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 幽冥体节激光 — 由蠕虫体节同步发射的预告型激光。
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

            if (IsActive) {
                Vector2 step = Dir;
                for (int i = 0; i < 18; i++) {
                    if (!Main.rand.NextBool(2))
                        continue;
                    Vector2 pos = Projectile.Center + step * (i * 50f + Main.rand.NextFloat(50f));
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = step.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.5f, 1.5f);
                }
                Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.6f);
            }
        }

        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox) {
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
            SpriteBatch sb = Main.spriteBatch;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Dir * BeamLength;

            // BeamGrad 缺失(加载失败)时回退到旧的 dust 光束, 保证激光帘幕始终可读。
            bool hasBeam = ACMShaders.BeamGrad != null && ACMShaders.NoiseTexture != null;

            if (!IsActive) {
                // 细线预告 — 幽蓝紫(非致命色)渐强 (§6.1 红只留给实伤)
                float t = Timer / (float)TelegraphTime;
                Color edge = Color.Lerp(TelegraphColors.NetherViolet, AwakeningNetherHelper.AwakeningPurple, t);
                if (hasBeam) {
                    ACMShaders.DrawBeam(start, end, 2f + t * 4f, Color.Lerp(edge, Color.White, 0.35f), edge,
                        0.25f + 0.45f * t, flowSpeed: 2.0f, flowScale: 2.4f);
                }
                else {
                    AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, edge * (0.3f + 0.4f * t), 4f + t * 4f, Timer);
                }
            }
            else {
                // 实体激光帘幕 — 致命(红芯紫边), BeamGrad 流动 + 过曝芯
                float at = (Timer - TelegraphTime) / (float)ActiveTime;
                float widthMod = MathF.Sin(at * MathF.PI) * 0.7f + 0.4f;
                if (hasBeam) {
                    ACMShaders.DrawBeam(start, end, 26f * widthMod, TelegraphColors.Lethal, AwakeningNetherHelper.AwakeningPurple,
                        1f, flowSpeed: 2.6f, flowScale: 2.0f, coreGlow: 1.4f);
                    ACMShaders.DrawBeam(start, end, 10f * widthMod, Color.White, TelegraphColors.Lethal,
                        1f, flowSpeed: 2.6f, flowScale: 2.0f, coreGlow: 1.8f);
                }
                else {
                    Color beam = AwakeningNetherHelper.AwakeningPurple;
                    AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, beam, 22f * widthMod, Timer, true);
                    AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, Color.Lerp(beam, Color.White, 0.5f), 8f * widthMod, Timer, true);
                }
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
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, Projectile.Center);
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

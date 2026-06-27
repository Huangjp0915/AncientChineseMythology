using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨念弹 - 怨灵的主要追踪弹幕
    /// </summary>
    public class SpectreWraithBolt : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase = 0f;
        private float homingStrength = 0.03f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Projectile.scale = 0.3f;
            pulsePhase += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微追踪
            Player target = FindTarget();
            if (target != null && Projectile.timeLeft > 200) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            }

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), dustType);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 100;
            }

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreCyan.ToVector3() * 0.3f);
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 600f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreCyan, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.5f + progress * 0.5f), SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;
            Color mainColor = SpectreHelper.SpectreCyan;

            // 光晕
            Color glowColor = mainColor;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.4f,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.3f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 120);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 30f, 1, 8);
        }
    }

    /// <summary>
    /// 灵魂链条 - 连接攻击弹幕
    /// </summary>
    public class SpectreSoulChain : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase = 0f;
        private Vector2 targetPos;
        private bool initialized = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 100;
        }

        public override void AI() {
            if (!initialized) {
                targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                initialized = true;
            }

            pulsePhase += 0.15f;

            // 向目标位置移动
            Vector2 toTarget = (targetPos - Projectile.Center).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(Projectile.Center, targetPos);

            if (dist > 30f) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.08f);
            }
            else {
                // 到达目标后消散
                Projectile.velocity *= 0.9f;
                Projectile.alpha += 5;
                if (Projectile.alpha > 255) {
                    Projectile.Kill();
                }
            }

            Projectile.rotation += 0.1f;

            // 链条粒子
            SpectreHelper.CreateSoulChainParticles(Projectile.Center, targetPos, 0.5f);

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreYellow.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制链条
            if (Projectile.oldPos.Length > 5 && Projectile.oldPos[5] != Vector2.Zero) {
                Vector2 chainStart = Projectile.oldPos[Math.Min(10, Projectile.oldPos.Length - 1)] + Projectile.Size / 2;
                SpectreHelper.DrawSoulChain(sb, chainStart, Projectile.Center,
                    SpectreHelper.SpectreYellow, 6f, pulsePhase * 60f);
            }

            // 绘制核心
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            SpectreHelper.DrawSpectreCore(sb, Projectile.Center,
                SpectreHelper.SpectreYellow, SpectreHelper.SpectreGold,
                pulse, pulsePhase);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slow, 90);
            UnderworldField.AddSoulErosion(target, 1); // 怨链命中挂魂蚀 DoT (§04 身份层)
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 40f, 2, 10);
        }
    }

    /// <summary>
    /// 灵魂球 - 环形弹幕
    /// </summary>
    public class SpectreSoulOrb : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private float pulsePhase = 0f;
        private int ColorType => (int)Projectile.ai[0]; // 0=青色, 1=黄色

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 100;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            Projectile.rotation += 0.08f;

            // 轻微加速
            if (Projectile.velocity.Length() < 12f) {
                Projectile.velocity *= 1.01f;
            }

            // 粒子
            if (Main.rand.NextBool(3)) {
                int dustType = ColorType == 0 ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.08f;
            }

            Color lightColor = ColorType == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.25f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            Color orbColor = ColorType == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = orbColor * progress * 0.4f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.4f + progress * 0.6f), SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;

            // 光晕
            Color glowColor = orbColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.5f,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.4f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, orbColor,
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (ColorType == 0) {
                target.AddBuff(BuffID.Frostburn, 90);
            }
            else {
                target.AddBuff(BuffID.OnFire, 90);
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                int dustType = ColorType == 0 ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 哀嚎波 - 大范围扩散弹幕
    /// </summary>
    public class SpectreWailingWave : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreWave";

        private float pulsePhase = 0f;
        private float growthScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 200;
            Projectile.alpha = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            growthScale = MathHelper.Lerp(growthScale, 1.5f, 0.02f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 逐渐加速
            if (Projectile.velocity.Length() < 14f) {
                Projectile.velocity *= 1.015f;
            }

            // 波形运动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(pulsePhase * 0.8f) * 3f;
            Projectile.position += perpendicular * wave;

            // 粒子
            SpectreHelper.CreateSpectreTrail(Projectile.Center, Projectile.velocity, growthScale);

            // 发光
            Color lightColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.4f * growthScale);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 多层拖尾
            for (int layer = 0; layer < 2; layer++) {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float progress = 1f - i / (float)Projectile.oldPos.Length;
                    float layerAlpha = progress * (layer == 0 ? 0.25f : 0.4f);

                    Color trailColor = layer == 0
                        ? SpectreHelper.SpectreDeepCyan
                        : Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, progress);
                    trailColor *= layerAlpha;
                    trailColor.A = 0;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                    float trailScale = growthScale * (0.4f + progress * 0.6f) * (layer == 0 ? 1.5f : 1f);

                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.4f);

            // 光晕
            Color glowColor = mainColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.4f,
                Projectile.rotation, origin, growthScale * pulse * 1.5f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, growthScale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Confused, 60);
            target.AddBuff(BuffID.Chilled, 120);
            UnderworldField.AddSoulErosion(target, 1); // 哀嚎波命中挂魂蚀 DoT (§04 身份层)
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.2f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 50f * growthScale, 2, 12);
        }
    }

    /// <summary>
    /// 鬼火灯笼锚点 (Wraith Lantern Anchor) —— 《怨念清算》的反制装置。
    /// 怨灵在竞技场撒下的鬼火灯，玩家站到灯笼附近即可"清账/断视线"：
    /// 周期性降低怨灵怨念 (ReduceGrudge) + 净化自身魂蚀, 从而压低终幕报复波规模。
    /// 非伤害实体 (hostile=false, friendly=false)；纯锚点 + 可读绿色安全脉冲。
    /// 与可选掉落武器 <see cref="Items.WraithLantern"/> 同主题 (灯/链/怨念)。
    /// </summary>
    public class SpectreLanternAnchor : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private const float CleanseRadius = 200f;
        private float PulsePhase;

        private ref float OwnerNpc => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override void AI() {
            PulsePhase += 0.09f;
            Projectile.velocity *= 0.9f;

            // 锚点存活绑定怨灵：Boss 不在则消散
            int idx = (int)OwnerNpc;
            if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active ||
                Main.npc[idx].type != ModContent.NPCType<Spectre>()) {
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 30);
            }

            // 清账反制：附近玩家每 30t 降怨念账 (账面 = 终幕报复规模)。服务器权威。
            if (Main.netMode != NetmodeID.MultiplayerClient && Projectile.timeLeft % 30 == 0 &&
                idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                for (int p = 0; p < Main.maxPlayers; p++) {
                    Player pl = Main.player[p];
                    if (!pl.active || pl.dead) continue;
                    if (pl.Distance(Projectile.Center) < CleanseRadius) {
                        UnderworldField.ReduceGrudge(Main.npc[idx], 1); // 清账 = 压低报复波
                        break;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreYellow.ToVector3() * 0.6f);
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(CleanseRadius, CleanseRadius);
                Dust d = Dust.NewDustPerfect(pos, DustID.GreenTorch);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 1.6f;
                d.alpha = 120;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 绿色安全脉冲环 (可站区, 非红)
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            Color safe = TelegraphColors.GhostGreen;
            safe.A = 0;
            int ringSeg = 28;
            float r = CleanseRadius * (0.7f + MathF.Sin(PulsePhase) * 0.06f);
            for (int i = 0; i < ringSeg; i++) {
                float a = MathHelper.TwoPi * i / ringSeg;
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * r - Main.screenPosition;
                sb.Draw(tex, pos, null, safe * 0.22f, a, origin, 0.32f, SpriteEffects.None, 0);
            }

            // 灯体
            SpectreHelper.DrawSpectreCore(sb, Projectile.Center,
                SpectreHelper.SpectreYellow, SpectreHelper.SpectreGold, 0.7f, PulsePhase);
            return false;
        }
    }

    /// <summary>
    /// 冤魂幻影突袭 (Phantom Rush) —— 《怨念清算》终幕镜像招：从玩家久留象限俯冲。
    /// 短前摇 (青白 telegraph) → 沿固定方向高速贯穿一次, 末段染红 (致命源 §6.1)。
    /// </summary>
    public class SpectrePhantomRush : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private const int WindupTime = 36;

        private ref float TargetPlayer => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private bool dashed;
        private Vector2 dashDir;
        private float pulse;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 60;
        }

        public override void AI() {
            pulse += 0.13f;
            Timer++;

            if (!dashed) {
                // 前摇：缓慢漂移并锁定方向 (青白拉长光线见 PreDraw)
                Projectile.velocity *= 0.92f;
                int t = (int)TargetPlayer;
                if (t >= 0 && t < Main.maxPlayers && Main.player[t].active && !Main.player[t].dead)
                    dashDir = (Main.player[t].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                else if (Projectile.velocity != Vector2.Zero)
                    dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

                if (Timer >= WindupTime) {
                    dashed = true;
                    Projectile.velocity = dashDir * 26f;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.2f }, Projectile.Center);
                }
            }
            else {
                Projectile.velocity *= 0.985f;
                if (!Main.dedServ)
                    SpectreHelper.CreateSpectreTrail(Projectile.Center, Projectile.velocity, 1.6f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreRage.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 前摇青白预告线 → 末段红 (致命)
            if (!dashed) {
                float prog = Timer / WindupTime;
                bool imminent = prog > 0.65f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                Color edge = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dashDir * 520f,
                    MathHelper.Lerp(6f, 16f, prog), core, edge, 0.3f + 0.6f * prog);
            }

            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color tc = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreRage, progress) * progress * 0.5f;
                tc.A = 0;
                Vector2 dp = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, dp, null, tc, Projectile.oldRot[i], origin, 0.5f * (0.5f + progress * 0.5f), SpriteEffects.None, 0);
            }

            float p = 1f + MathF.Sin(pulse) * 0.15f;
            Color body = dashed ? SpectreHelper.SpectreRage : SpectreHelper.SpectreCyan;
            Color glow = body; glow.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glow * 0.5f, Projectile.rotation, origin, 0.55f * p * 1.3f, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, body, Projectile.rotation, origin, 0.55f * p, SpriteEffects.None, 0);
            return false;
        }

        public override bool CanHitPlayer(Player target) => dashed; // 前摇无伤, 只在俯冲时致命

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.3f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 冤魂审判·清算波 (Reckoning Wave) —— 《怨念清算》终幕的<b>唯一</b>扩张报复波。
    /// 半径/速度/伤害窗随怨念归一化 (ai0) 放大：清账成功→弱而窄; 积怨过重→宽而快。
    /// 取代旧 FinalGrudge 永久喷射 —— 每个审判循环只放一次。
    /// </summary>
    public class SpectreReckoningWave : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreWave";

        private ref float Grudge => ref Projectile.ai[0];

        private float expand;
        private float pulse;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.alpha = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        // 环形波的命中：以可见环带判定 (而非整圆), 给玩家"穿环"空间
        private float Radius => expand;
        private float BandHalf => 70f;

        public override void AI() {
            pulse += 0.14f;
            float g = MathHelper.Clamp(Grudge, 0f, 1f);
            float speed = MathHelper.Lerp(9f, 17f, g); // 怨念越高越快
            expand += speed;

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreRage.ToVector3() * 0.6f);
            if (!Main.dedServ) {
                int seg = 40;
                for (int i = 0; i < seg; i++) {
                    if (!Main.rand.NextBool(4)) continue;
                    float a = MathHelper.TwoPi * i / seg;
                    Vector2 pos = Projectile.Center + a.ToRotationVector2() * expand;
                    Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.YellowTorch : DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = a.ToRotationVector2() * 2f;
                }
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 c = Projectile.Center;
            Vector2 closest = Vector2.Clamp(c, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            float d = Vector2.Distance(c, closest);
            float maxR = MathHelper.Lerp(420f, 760f, MathHelper.Clamp(Grudge, 0f, 1f));
            if (Radius > maxR) return false;
            return MathF.Abs(d - Radius) < BandHalf;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
            UnderworldField.AddNetherDecree(target, 1); // 终幕清算记一笔冥律
        }

        public override bool PreDraw(ref Color lightColor) {
            float g = MathHelper.Clamp(Grudge, 0f, 1f);
            // 报复波环：宽度/红度随怨念 (致命源 → 红)。双环叠绘 (外暗内亮)。
            Color outer = Color.Lerp(SpectreHelper.SpectreCyan, TelegraphColors.Lethal, g);
            float width = MathHelper.Lerp(20f, 48f, g);
            SpectreHelper.DrawEnergyWave(Main.spriteBatch, Projectile.Center, expand, width, outer, 0.85f);
            SpectreHelper.DrawEnergyWave(Main.spriteBatch, Projectile.Center, expand, width * 0.5f,
                SpectreHelper.SpectreYellow, 0.7f);
            return false;
        }
    }
}

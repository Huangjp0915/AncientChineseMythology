using AncientChineseMythology.Helpers;
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
    /// 怨念弹 - 怨灵的主要压制弹幕。
    /// V3: 出膛 24f 内 40%→100% 速度爬升 (换幕防贴脸阀门); 追踪只在前 60f 生效。
    /// </summary>
    public class SpectreWraithBolt : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase;
        private const float HomingStrength = 0.03f;

        private ref float Age => ref Projectile.localAI[0];

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
            Projectile.scale = 0.3f;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            Age++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 出膛加速阀门: 前 24f 从 40% 爬到 100%
            if (Age <= 24f) {
                float ramp = MathHelper.Lerp(0.4f, 1f, Age / 24f);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * 8.6f * ramp;
            }

            // 轻追踪 (仅前 60f — 之后是"死的账", 可绕)
            Player target = FindTarget();
            if (target != null && Age > 24f && Age < 60f) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), HomingStrength);
            }

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

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;
            Color mainColor = SpectreHelper.SpectreCyan;

            // 出膛期半透明 (视觉与低速威胁一致)
            float ramp = MathHelper.Clamp(Age / 24f, 0.45f, 1f);

            Color glowColor = mainColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.4f * ramp,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.3f, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor * ramp,
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
    /// 勾魂链 - 甩向锁定落点的链头。
    /// V3: 链体实时从怨灵本体绘到链头 (BeamGrad 流动光束) — "甩出去的链仍连着账主"。
    /// 命中挂魂蚀 (UnderworldField)。
    /// </summary>
    public class SpectreSoulChain : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase;
        private Vector2 targetPos;
        private bool initialized;

        private ref float OwnerCache => ref Projectile.localAI[0]; // 怨灵 NPC 索引缓存 (+1 存储, 0=未找)

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

        private NPC FindOwnerBoss() {
            int cached = (int)OwnerCache - 1;
            int type = ModContent.NPCType<Spectre>();
            if (cached >= 0 && cached < Main.maxNPCs && Main.npc[cached].active && Main.npc[cached].type == type)
                return Main.npc[cached];
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == type) {
                    OwnerCache = i + 1;
                    return Main.npc[i];
                }
            }
            return null;
        }

        public override void AI() {
            if (!initialized) {
                targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                initialized = true;
            }

            pulsePhase += 0.15f;

            Vector2 toTarget = (targetPos - Projectile.Center).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(Projectile.Center, targetPos);

            if (dist > 30f) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.08f);
            }
            else {
                // 到达落点: 定住片刻再消散 (链绷直的一拍)
                Projectile.velocity *= 0.85f;
                Projectile.alpha += 6;
                if (Projectile.alpha > 255)
                    Projectile.Kill();
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), DustID.YellowTorch);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.08f;
            }

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreYellow.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float fade = 1f - Projectile.alpha / 255f;

            // 链体: 本体 → 链头 (双层流动光束; 超距不画避免拉花)
            NPC owner = FindOwnerBoss();
            if (owner != null && owner.Distance(Projectile.Center) < 1100f) {
                ACMShaders.DrawBeam(owner.Center, Projectile.Center, 7f,
                    SpectreHelper.SpectreYellow with { A = 150 },
                    SpectreHelper.SpectreDarkGreen with { A = 90 }, 0.7f * fade, 1.6f, 2.2f);
                ACMShaders.DrawBeam(owner.Center, Projectile.Center, 3f,
                    Color.Lerp(SpectreHelper.SpectreGold, Color.White, 0.2f) with { A = 220 },
                    SpectreHelper.SpectreYellow with { A = 140 }, 0.85f * fade, 2.4f, 2.6f);
            }

            // 链头怨火
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            SpectreHelper.DrawSpectreCore(sb, Projectile.Center,
                SpectreHelper.SpectreYellow * fade, SpectreHelper.SpectreGold * fade,
                pulse, pulsePhase);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slow, 90);
            UnderworldField.AddSoulErosion(target, 1); // 怨链命中挂魂蚀 DoT
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 40f, 2, 10);
        }
    }

    /// <summary>
    /// 灵魂球 - 风暴环形弹幕。V3: 出膛 20f 加速阀门 + 拖尾亮度随速度门控。
    /// </summary>
    public class SpectreSoulOrb : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private float pulsePhase;
        private int ColorType => (int)Projectile.ai[0]; // 0=青色, 1=黄色

        private ref float Age => ref Projectile.localAI[0];

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
            Age++;
            Projectile.rotation += 0.08f;

            // 出膛加速阀门: 前 20f 从 35% 爬升, 之后缓慢加速到 12
            if (Age <= 20f) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * 7.4f * MathHelper.Lerp(0.35f, 1f, Age / 20f);
            }
            else if (Projectile.velocity.Length() < 12f) {
                Projectile.velocity *= 1.01f;
            }

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
            float ramp = MathHelper.Clamp(Age / 20f, 0.4f, 1f);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = orbColor * progress * 0.4f * ramp;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.4f + progress * 0.6f) * 0.16f, SpriteEffects.None, 0);
            }

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;

            Color glowColor = orbColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.5f * ramp,
                Projectile.rotation, origin, Projectile.scale * pulse * 0.22f, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, orbColor * ramp,
                Projectile.rotation, origin, Projectile.scale * pulse * 0.16f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (ColorType == 0)
                target.AddBuff(BuffID.Frostburn, 90);
            else
                target.AddBuff(BuffID.OnFire, 90);
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
    /// 哀嚎波 - 尖啸环扩散弹幕。
    /// V3: 出膛 20f 从 16% 爬升到全速 (环从 Boss 身边"长"出来, 贴身反而安全);
    /// 绘制改用 SpectreCore 魂焰贴图 (旧 SpectreWave 近白图几乎不可见)。
    /// </summary>
    public class SpectreWailingWave : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase;
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 190;
            Projectile.alpha = 80;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            Age++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            if (Age <= 20f) {
                // 出膛爬升: 尖啸环从身边慢慢"胀"出去
                Projectile.velocity = dir * MathHelper.Lerp(2.2f, 13.5f, SpectreHelper.SmoothStep(Age / 20f));
            }
            else {
                // 轻微波形横摆 (幽魂的嚎不走直线)
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                Projectile.position += perp * MathF.Sin(pulsePhase * 0.8f + Projectile.whoAmI) * 2.2f;
            }

            if (Main.rand.NextBool(3))
                SpectreHelper.CreateSpectreTrail(Projectile.Center, Projectile.velocity, 1f);

            Color lightColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float ramp = MathHelper.Clamp(Age / 20f, 0.3f, 1f);

            // 双层拖尾 (魂焰彗尾)
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreCyan, progress)
                    * (progress * 0.45f * ramp);
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    0.32f * (0.4f + progress * 0.6f), SpriteEffects.None, 0);
            }

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.4f);

            Color glowColor = mainColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.5f * ramp,
                Projectile.rotation, origin, 0.4f * pulse, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor * ramp,
                Projectile.rotation, origin, 0.3f * pulse, SpriteEffects.None, 0);
            // 白热芯
            Color hot = Color.White;
            hot.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, hot * 0.35f * ramp,
                Projectile.rotation, origin, 0.16f * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Confused, 60);
            target.AddBuff(BuffID.Chilled, 120);
            UnderworldField.AddSoulErosion(target, 1); // 哀嚎波命中挂魂蚀 DoT
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.2f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 40f, 2, 10);
        }
    }

    /// <summary>
    /// 鬼火灯笼锚点 (Wraith Lantern Anchor) —— 反制装置 + 哀嚎安全灯道的"灯"。
    /// 玩家站到灯笼附近即可"清账"：周期性降低怨灵怨念, 压低终幕报复波规模;
    /// 哀嚎尖啸的安全缝永远朝向最近的灯 ("顺着灯走")。
    /// 非伤害实体; 死亡演出中被逐盏熄灭送葬。
    /// </summary>
    public class SpectreLanternAnchor : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private const float CleanseRadius = 200f;
        private float PulsePhase;
        private float lightUp; // 点亮进度 (凝形)

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
            lightUp = MathHelper.Clamp(lightUp + 0.04f, 0f, 1f);
            Projectile.velocity *= 0.9f;

            // 锚点存活绑定怨灵：Boss 不在则消散
            int idx = (int)OwnerNpc;
            if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active ||
                Main.npc[idx].type != ModContent.NPCType<Spectre>()) {
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 30);
            }

            // 清账反制：附近玩家每 30t 降怨念账。服务器权威。
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

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreYellow.ToVector3() * 0.6f * lightUp);
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
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 绿色安全脉冲环 (可站区, 非红)
            Color safe = TelegraphColors.GhostGreen;
            safe.A = 0;
            int ringSeg = 28;
            float r = CleanseRadius * (0.7f + MathF.Sin(PulsePhase) * 0.06f) * lightUp;
            for (int i = 0; i < ringSeg; i++) {
                float a = MathHelper.TwoPi * i / ringSeg + PulsePhase * 0.2f;
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * r - Main.screenPosition;
                sb.Draw(tex, pos, null, safe * 0.22f * lightUp, a, origin, 0.12f, SpriteEffects.None, 0);
            }

            // 灯焰逐亮 + 呼吸 (熄灭时随 timeLeft 收缩)
            float dying = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float glow = lightUp * dying;
            SpectreHelper.DrawSpectreCore(sb, Projectile.Center,
                SpectreHelper.SpectreYellow * glow, SpectreHelper.SpectreEmber * glow,
                0.7f * (0.6f + glow * 0.4f), PulsePhase);
            return false;
        }

        public override void OnKill(int timeLeft) {
            // 灯灭: 一缕青烟 (死亡送葬节拍的"噗")
            if (Main.dedServ) return;
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    Main.rand.NextBool() ? DustID.Smoke : DustID.YellowTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3.5f));
            }
        }
    }

    /// <summary>
    /// 冤魂幻影突袭 (Phantom Rush) —— 怨灵自身的鬼影。
    /// 两种模式: ai0 ≥ 0 = 追踪玩家 (审判镜像); ai0 = -1 = 定向重演 (沿出生速度方向, 《幻影重演》)。
    /// 短前摇 (青白 telegraph, 末段红) → 高速贯穿一次。用本体贴图绘制 — 它就是怨灵的残影。
    /// </summary>
    public class SpectrePhantomRush : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "Spectre";

        private const int WindupTime = 36;
        private const float DashSpeed = 27f;

        private ref float TargetPlayer => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private bool dashed;
        private Vector2 dashDir = Vector2.UnitX;
        private float pulse;

        private bool ReplayMode => TargetPlayer < 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 220;
            Projectile.alpha = 60;
        }

        public override void AI() {
            pulse += 0.13f;
            Timer++;

            if (!dashed) {
                if (ReplayMode) {
                    // 定向重演: 方向锁定于出生速度, 原地凝聚
                    if (Projectile.velocity != Vector2.Zero)
                        dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Projectile.velocity *= 0.9f;
                }
                else {
                    // 追踪模式: 前摇缓慢漂移并锁定方向
                    Projectile.velocity *= 0.92f;
                    int t = (int)TargetPlayer;
                    if (t >= 0 && t < Main.maxPlayers && Main.player[t].active && !Main.player[t].dead)
                        dashDir = (Main.player[t].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    else if (Projectile.velocity != Vector2.Zero)
                        dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                }

                // 凝聚魂缕 (前摇可读)
                if (!Main.dedServ && Timer % 3 == 0) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(46, 46);
                    var d = Dust.NewDustPerfect(pos, DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 4f;
                }

                if (Timer >= WindupTime) {
                    dashed = true;
                    Projectile.velocity = dashDir * DashSpeed;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.2f }, Projectile.Center);
                }
            }
            else {
                Projectile.velocity *= 0.988f;
                if (!Main.dedServ)
                    SpectreHelper.CreateSpectreTrail(Projectile.Center, Projectile.velocity, 1.6f);
                // 冲刺后 60f 溶散
                if (Timer > WindupTime + 60)
                    Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreRage.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 前摇青白预告线 → 末段红 (致命)
            if (!dashed) {
                float prog = Timer / WindupTime;
                bool imminent = prog > 0.65f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                Color edgeC = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dashDir * 560f,
                    MathHelper.Lerp(6f, 16f, prog), core, edgeC, 0.3f + 0.6f * prog);
            }

            // 本体贴图的鬼影 (SpectreVeil: 高虚相 + 冲刺拖影) — 它就是怨灵的幻影
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects flip = dashDir.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float grow = MathHelper.Clamp(Timer / (float)WindupTime, 0.2f, 1f);
            Color tint = dashed ? SpectreHelper.SpectreRage : SpectreHelper.SpectreCyan;

            if (SpectreHelper.BeginVeilBatch(sb)) {
                if (dashed) {
                    for (int i = 9; i >= 3; i -= 3) {
                        if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;
                        float prog = 1f - i / 12f;
                        SpectreHelper.ApplyVeilParams(1f, 0.15f, 0.22f * prog, 0f,
                            new Vector2(dashDir.X * (flip == SpriteEffects.None ? 1f : -1f), dashDir.Y), 0.7f,
                            tint, 0.55f, tint, 0.7f);
                        sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition, null,
                            Color.White, 0f, origin, 0.8f * grow, flip, 0);
                    }
                }

                SpectreHelper.ApplyVeilParams(dashed ? 0.55f : 0.85f, dashed ? 0f : (1f - grow) * 0.6f,
                    0.9f, dashed ? 0.9f : 0.5f,
                    new Vector2(dashDir.X * (flip == SpriteEffects.None ? 1f : -1f), dashDir.Y),
                    dashed ? 1f : 0f, tint, 0.55f, tint, 0.9f);
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White,
                    0f, origin, 0.8f * grow, flip, 0);
                SpectreHelper.EndVeilBatch(sb);
            }
            else {
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, tint * 0.6f * grow,
                    0f, origin, 0.8f * grow, flip, 0);
            }
            return false;
        }

        public override bool CanHitPlayer(Player target) => dashed; // 前摇无伤, 只在俯冲时致命

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            SpectreHelper.CreateSpectreVortex(Projectile.Center, 60f, 1f, 16);
        }
    }

    /// <summary>
    /// 冤魂审判·清算波 (Reckoning Wave) —— 终幕的<b>唯一</b>扩张报复波。
    /// 半径/速度/伤害窗随怨念归一化 (ai0) 放大：清账成功→弱而窄; 积怨过重→宽而快。
    /// V3: 绘制改用顶点双环冲击波 (DrawShockwaveRing), 环带命中判定保留 "穿环" 空间。
    /// </summary>
    public class SpectreReckoningWave : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

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
            float maxR = MathHelper.Lerp(420f, 760f, g);
            float fade = MathHelper.Clamp((maxR - expand) / 120f, 0f, 1f) * MathHelper.Clamp(expand / 60f, 0f, 1f);
            if (fade <= 0.02f)
                return false;

            // 报复波双环: 外环红度随怨念 (致命源 → 红), 内环纸钱黄; 呼吸厚度
            Color outer = Color.Lerp(SpectreHelper.SpectreCyan, TelegraphColors.Lethal, g);
            float width = MathHelper.Lerp(22f, 46f, g) * (1f + MathF.Sin(pulse) * 0.08f);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, expand, width, 0.85f * fade,
                outer, Color.Lerp(outer, SpectreHelper.SpectreDeepCyan, 0.6f));
            WeaponVFX.DrawShockwaveRing(Projectile.Center, expand, width * 0.45f, 0.7f * fade,
                SpectreHelper.SpectreYellow, SpectreHelper.SpectreGold);
            return false;
        }
    }
}

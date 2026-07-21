using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.Swords;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 莫邪虚剑 — 干将莫邪之"青"雌剑 (青蓝半透明虚影, 溶解显形/消散)。
    /// ai[0]=模式: 0 = 影随斩 (主剑每段爆发后自动补斩, velocity 承载 X=主剑朝向 / Y=扫向, ai[1]=主剑初始角);
    ///            1 = 寻敌斩 (右键未满鸣: 飞向 ai[1] 目标 (无目标沿 velocity 方向 240px) 后旋斩一轮)。
    /// 重做旧版"瞬移挂机追踪剑": 影随让每一段连击自带双剑叙事, 寻敌斩保留原右键 DNA 但全程可读。
    /// </summary>
    public class GanJiangSwordProj_2 : ModProjectile
    {
        private const float Backswing = 0.3f;
        private const int ArcSegments = 20;

        // 影随斩节奏
        private const int EchoPrep = 4;
        private const int EchoExec = 5;
        private const int EchoFade = 8;
        private const int EchoLife = EchoPrep + EchoExec + EchoFade;

        // 寻敌斩节奏
        private const int SeekFly = 16;
        private const int SeekSpin = 14;
        private const int SeekFade = 10;
        private const int SeekLife = SeekFly + SeekSpin + SeekFade;

        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";

        private Player Owner => Main.player[Projectile.owner];
        private bool IsSeek => Projectile.ai[0] >= 1f;
        private ref float DataSlot => ref Projectile.ai[1]; // echo: 主剑初始角 / seek: 目标 whoAmI

        private readonly ColoredVertex[] _arcVerts = new ColoredVertex[(ArcSegments + 1) * 2];

        private float progress;      // 已扫角度
        private float spinBase;      // 寻敌斩起始角
        private Vector2 anchor;      // 旋斩锚点
        private bool spinStarted;
        private bool initialized;

        private int Age => (IsSeek ? SeekLife : EchoLife) - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.timeLeft = EchoLife;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!initialized) {
                initialized = true;
                if (IsSeek) {
                    Projectile.timeLeft = SeekLife;
                    anchor = Projectile.Center;
                }
                SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.4f, Volume = 0.55f }, Projectile.Center);
            }

            if (IsSeek)
                SeekAI();
            else
                EchoAI();
        }

        // ── 影随斩: 悬于玩家侧后, 顺主剑轨迹镜像补斩 ──
        private void EchoAI() {
            float mainDir = Projectile.velocity.X >= 0f ? 1f : -1f;
            float sweepSign = Projectile.velocity.Y >= 0f ? 1f : -1f;
            anchor = Owner.MountedCenter + new Vector2(-mainDir * 14f, -22f);
            Projectile.Center = anchor;

            int age = Age;
            float range = 2.4f; // 虚剑固定标准弧 (主剑旋斩时反向, 合成十字)

            if (age < EchoPrep) {
                progress = -Backswing;
            }
            else if (age < EchoPrep + EchoExec) {
                float t = (age - EchoPrep + 1) / (float)EchoExec;
                float ease = 1f - MathF.Pow(1f - t, 8f);
                progress = MathHelper.Lerp(-Backswing, range, ease);
                if (age == EchoPrep)
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.5f, Volume = 0.65f }, anchor);
            }
            // fade 期保持角度

            Projectile.rotation = DataSlot + mainDir * sweepSign * progress;

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 tip = anchor + Projectile.rotation.ToRotationVector2() * 80f;
                Dust d = Dust.NewDustPerfect(tip, DustID.IceTorch, Main.rand.NextVector2Circular(1f, 1f), 150, default, 0.9f);
                d.noGravity = true;
            }
        }

        // ── 寻敌斩: 飞向目标 → 旋斩一轮 → 消散 ──
        private void SeekAI() {
            int age = Age;
            NPC target = DataSlot >= 0f && DataSlot < Main.maxNPCs ? Main.npc[(int)DataSlot] : null;
            bool targetValid = target != null && target.active && !target.friendly;

            if (age < SeekFly && !spinStarted) {
                Vector2 dest = targetValid
                    ? target.Center + new Vector2(0f, -10f)
                    : anchor + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 240f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, dest, 0.24f);
                Projectile.rotation = (dest - Projectile.Center).SafeNormalize(Vector2.UnitX).ToRotation();

                // 提前到位 → 立刻起旋 (不等自己的时刻表)
                if (Vector2.DistanceSquared(Projectile.Center, dest) < 42f * 42f || age == SeekFly - 1)
                    StartSpin();
            }
            else if (spinStarted && age < SeekFly + SeekSpin) {
                float t = MathHelper.Clamp((age - SeekFly + 1) / (float)SeekSpin, 0f, 1f);
                float ease = 1f - MathF.Pow(1f - t, 4f);
                progress = MathHelper.TwoPi * 0.9f * ease;
                Projectile.rotation = spinBase + progress;
                if (targetValid)
                    Projectile.Center = Vector2.Lerp(Projectile.Center, target.Center, 0.2f);
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                    DustID.IceTorch, Main.rand.NextVector2Circular(1.2f, 1.2f), 150, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.55f);
        }

        private void StartSpin() {
            if (spinStarted)
                return;
            spinStarted = true;
            spinBase = Projectile.rotation;
            // 时刻表快进到旋斩起点
            Projectile.timeLeft = SeekSpin + SeekFade;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.9f }, Projectile.Center);
        }

        private bool InDamageWindow {
            get {
                int age = Age;
                if (IsSeek)
                    return spinStarted && age < SeekFly + SeekSpin;
                return age >= EchoPrep && age < EchoPrep + EchoExec;
            }
        }

        public override bool? CanDamage() => InDamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = IsSeek ? Projectile.Center : anchor;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * 100f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dedServ) {
                for (int i = 0; i < 7; i++) {
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.IceTorch,
                        Main.rand.NextVector2Circular(4f, 5f), 100, default, 1f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.7f;
                }
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gem, scale: IsSeek ? 1f : 0.8f, owner: Projectile.owner);

            // 莫邪同奏剑鸣
            if (Projectile.owner == Main.myPlayer)
                Owner.GetModPlayer<GanJiangResonancePlayer>().AddResonance(1);
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;

            // 溶解显形/消散包络
            float dissolve;
            float alpha;
            if (IsSeek) {
                float fadeStart = SeekLife - SeekFade;
                dissolve = age < 6 ? MathHelper.Lerp(0.9f, 0.2f, age / 6f)
                    : age > fadeStart ? MathHelper.Lerp(0.2f, 1f, (age - fadeStart) / (float)SeekFade) : 0.2f;
                alpha = 1f - MathHelper.Clamp((age - fadeStart) / (float)SeekFade, 0f, 1f);
            }
            else {
                int fadeStart = EchoPrep + EchoExec;
                dissolve = age < EchoPrep ? MathHelper.Lerp(0.9f, 0.25f, age / (float)EchoPrep)
                    : age >= fadeStart ? MathHelper.Lerp(0.25f, 1f, (age - fadeStart) / (float)EchoFade) : 0.25f;
                alpha = age >= fadeStart ? 1f - (age - fadeStart) / (float)EchoFade : 1f;
            }

            // 影随斩弧光 (青蓝, 专属着色器)
            if (!IsSeek && age >= EchoPrep)
                DrawEchoArc(alpha);

            // 虚剑本体: 溶解青蓝虚影
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float rotationOffset = MathHelper.PiOver4; // 贴图对角朝向补正 (中心原点旋转)
            WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, null,
                new Color(150, 215, 255) * (0.75f * alpha),
                Projectile.rotation + rotationOffset, tex.Size() * 0.5f, 1.1f,
                threshold: dissolve, intensity: alpha,
                edgeColor: new Color(140, 230, 255, 220), edgeWidth: 0.1f, noiseScale: 2.2f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.35f + 0.15f * alpha, new Color(110, 200, 255) * (0.5f * alpha));
            return false;
        }

        /// <summary>青蓝影随弧光 (与主剑同款着色器, 冷色系)。</summary>
        private void DrawEchoArc(float alpha) {
            Effect fx = WeaponVFX.GetEffect("GanJiangTwinArc");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null || Main.dedServ)
                return;

            float mainDir = Projectile.velocity.X >= 0f ? 1f : -1f;
            float sweepSign = Projectile.velocity.Y >= 0f ? 1f : -1f;
            float dirSign = mainDir * sweepSign;
            float range = 2.4f;
            float startAngle = DataSlot - dirSign * Backswing;
            float fullSpan = dirSign * (range + Backswing);
            float prog = MathHelper.Clamp((progress + Backswing) / (range + Backswing), 0f, 1f);

            Vector2 center = anchor - Main.screenPosition;
            for (int i = 0; i <= ArcSegments; i++) {
                float frac = i / (float)ArcSegments;
                Vector2 rot = (startAngle + fullSpan * frac).ToRotationVector2();
                _arcVerts[i * 2] = new ColoredVertex(center + rot * 30f, new Vector3(frac, 0f, 1f), Color.White);
                _arcVerts[i * 2 + 1] = new ColoredVertex(center + rot * 96f, new Vector3(frac, 1f, 1f), Color.White);
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(0.85f * alpha);
            fx.Parameters["uProgress"]?.SetValue(prog);
            fx.Parameters["uColorCore"]?.SetValue(new Color(200, 245, 255).ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(new Color(40, 140, 220).ToVector4());
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uTailLen"]?.SetValue(0.5f);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, _arcVerts, 0, _arcVerts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}

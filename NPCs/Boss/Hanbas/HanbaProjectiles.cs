using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    /// <summary>
    /// 旱魃弹幕体系 (V3 重做)。
    /// 核心修复: 旧版全部弹幕从未设 hostile=true (零伤害), 且判定与视觉不符;
    /// 本版全部补 hostile 并用 Colliding 使判定与视觉严格一致, 预警期无判定 (§6.1)。
    /// </summary>

    // ============================================================
    //  火球 — 焦目连珠 / 焦目双珠
    // ============================================================
    internal class HanbaFireBall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=计时; ai[1]=模式 (0=直线, 1=延迟缓追); ai[2]=兼容旧弯折参数(未用)。</summary>
        private ref float Timer => ref Projectile.ai[0];
        private ref float Mode => ref Projectile.ai[1];

        private const int HomingStart = 24;   // 缓追启动帧
        private const int HomingEnd = 190;    // 追踪熄灭 → 哑弹 (公平阀门)
        private const int LifeTime = 244;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.alpha = 200;
        }

        public static void KillAll() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HanbaFireBall>()) {
                    continue;
                }
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        // 显形前 12f 无判定 (出膛柔化, 防贴脸秒); 熄灭成哑弹后同样无判定
        public override bool? CanDamage() => Timer < 12 || Timer > HomingEnd + 24 ? false : null;

        public override void AI() {
            Timer++;

            // 出膛淡入
            if (Projectile.alpha > 0)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 25);

            bool burnedOut = Timer > HomingEnd;

            if (Mode == 1f && !burnedOut && Timer > HomingStart) {
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player is not null) {
                    // 缓追: 转向率恒定可读, 190f 后熄灭成哑弹 (公平阀门)
                    Vector2 targetVel = Projectile.SafeDirectionTo(player.Center) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVel, 0.02f);
                }
            }

            if (burnedOut) {
                Projectile.velocity *= 0.985f;
                Projectile.alpha = Math.Min(255, Projectile.alpha + 6);
            }

            Lighting.AddLight(Projectile.Center, HanbaVFX.EmberOrange.ToVector3() * 0.5f * Projectile.Opacity);

            if (Main.dedServ)
                return;

            // 火尾 (量 ∝ 速度, 熄灭期转灰烟)
            int dustCount = burnedOut ? 1 : 2;
            for (int i = 0; i < dustCount; i++) {
                int dustType = burnedOut ? DustID.Smoke : DustID.Torch;
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    dustType, -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default,
                    burnedOut ? 1.1f : Main.rand.NextFloat(1.2f, 2.0f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float fade = Projectile.Opacity;
            float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI) * 0.1f;
            // 双层柔光火核 (外焰橙 + 内芯金)
            HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, 0.9f * Projectile.scale * pulse, HanbaVFX.EmberOrange * (0.85f * fade));
            HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, 0.45f * Projectile.scale * pulse, HanbaVFX.SunGold * (0.9f * fade));
            return false;
        }
    }

    // ============================================================
    //  眼激光 — 鬼域焦笼 (BeamGrad 顶点带; 判定=线碰撞, 与束宽一致)
    // ============================================================
    [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hanbas/")]
    internal class HanbaLaser : ModProjectile
    {
        internal static Asset<Texture2D> UltimaRayEnd = null;
        internal static Asset<Texture2D> UltimaRayMid = null;
        internal static Asset<Texture2D> UltimaRayStart = null;

        /// <summary>眼位偏移 (随 Boss 旋转); 经 SendExtraAI 同步 (旧版漏同步导致多人激光集中于中心)。</summary>
        public Vector2 offsetData;
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        public ref float Weith => ref Projectile.localAI[0];
        public float Leng {
            get => Projectile.localAI[1];
            set => Projectile.localAI[1] = value;
        }

        // —— BeamGrad 原语接线: 0~1 生长进度同时驱动 alpha/宽度/预警→判定切换 ——
        protected virtual float BeamGrow => MathHelper.Clamp(Projectile.localAI[0] / 30f, 0f, 1f);
        protected virtual float BeamHalfWidth => Weith * 20f;
        protected virtual Color BeamCore => new(255, 150, 40);
        protected virtual Color BeamEdge => new(150, 12, 22);

        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 1120;
            Projectile.tileCollide = false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(offsetData.X);
            writer.Write(offsetData.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            offsetData.X = reader.ReadSingle();
            offsetData.Y = reader.ReadSingle();
        }

        public static void AllVanish() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HanbaLaser>()) {
                    continue;
                }
                proj.ai[1] = 1;
                proj.netUpdate = true;
            }
        }

        // 生长过半才有判定 (预警期只显致命色, §6.1 伤害窗口与视觉对齐)
        public override bool? CanDamage() => BeamGrow > 0.55f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float grow = BeamGrow;
            if (grow <= 0.55f)
                return false;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Leng + Projectile.width);
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamHalfWidth * 1.6f, ref point);
        }

        public override void AI() {
            Weith = Projectile.localAI[0] / 30f;
            Projectile.timeLeft = 1120;
            Projectile.rotation = Projectile.velocity.ToRotation();

            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (npc.Alives() && npc.ModNPC is Hanba boss) {
                Vector2 origin = boss.NPC.Center + offsetData.RotatedBy(boss.NPC.rotation);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(boss.NPC.rotation);

                Projectile.Center = origin;
                Projectile.rotation = dir.ToRotation();

                Leng = DistanceToRectEdge(origin, dir, boss.GetOrigPos(), 800, 800); //打到笼壁的长度

                if (!Main.dedServ && BeamGrow > 0.3f) {
                    Vector2 endPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * (Leng + Projectile.width);
                    for (int i = 0; i < 2; i++) {
                        Dust dust = Dust.NewDustPerfect(endPos, DustID.Torch,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(3), 150, Color.OrangeRed, 1.5f);
                        dust.noGravity = true;
                    }
                }
            }
            else {
                Projectile.ai[1] = 1f;
            }

            if (Projectile.ai[1] == 0) {
                if (Projectile.localAI[0] < 30) {
                    Projectile.localAI[0]++;
                }
            }
            else {
                if (Projectile.localAI[0] > 0) {
                    Projectile.localAI[0]--;
                }
                else {
                    Projectile.Kill();
                }
            }

            Time++;
        }

        public static float DistanceToRectEdge(Vector2 origin, Vector2 direction, Vector2 rectCenter, float sizeX, float sizeY) {
            float halfX = sizeX;
            float halfY = sizeY;

            float left = rectCenter.X - halfX;
            float right = rectCenter.X + halfX;
            float top = rectCenter.Y - halfY;
            float bottom = rectCenter.Y + halfY;

            direction = direction.SafeNormalize(Vector2.UnitY);

            float tMin = float.PositiveInfinity;

            if (direction.X != 0f) {
                float tx1 = (left - origin.X) / direction.X;
                float tx2 = (right - origin.X) / direction.X;
                foreach (float t in new[] { tx1, tx2 }) {
                    if (t > 0) {
                        float y = origin.Y + t * direction.Y;
                        if (y >= top && y <= bottom)
                            tMin = Math.Min(tMin, t);
                    }
                }
            }

            if (direction.Y != 0f) {
                float ty1 = (top - origin.Y) / direction.Y;
                float ty2 = (bottom - origin.Y) / direction.Y;
                foreach (float t in new[] { ty1, ty2 }) {
                    if (t > 0) {
                        float x = origin.X + t * direction.X;
                        if (x >= left && x <= right)
                            tMin = Math.Min(tMin, t);
                    }
                }
            }

            return float.IsInfinity(tMin) ? 2000f : tMin;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float grow = BeamGrow;
            if (grow <= 0.001f)
                return false;

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float length = Leng + Projectile.width;

            // 蓄力 telegraph: 生长前半段染纯红致命预警, 完全展开后回到身份色
            float warn = 1f - MathHelper.Clamp(grow / 0.5f, 0f, 1f);
            Color core = Color.Lerp(BeamCore, TelegraphColors.Lethal, warn * 0.65f);
            Color edge = Color.Lerp(BeamEdge, TelegraphColors.Lethal, warn);

            Vector2 endPos = Projectile.Center + dir.SafeNormalize(Vector2.UnitX) * length;
            ACMShaders.DrawBeam(Projectile.Center, endPos, BeamHalfWidth, core, edge, grow);
            return false;
        }
    }

    // ============================================================
    //  烈日灼柱 — 单向匀速扫射的金色太阳柱 (预警线→张开→扫过 110°)
    // ============================================================
    internal class HanbaBigLaser : HanbaLaser
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>扫射方向符号 (+1/-1), 服务器决定后经 SendExtraAI 同步。</summary>
        public float sweepDir = 1f;
        /// <summary>灼柱总扫幅 (弧度), 终章加宽。</summary>
        public float sweepArc = MathHelper.ToRadians(110);

        private const int TelegraphTime = 78;  // 预警细线时长 (Execution 级)
        private const int GrowTime = 26;       // 张开时长

        protected override float BeamGrow => MathHelper.Clamp(Projectile.localAI[2] / GrowTime, 0f, 1f);
        protected override float BeamHalfWidth => Weith * 24f;
        protected override Color BeamCore => new(255, 225, 110);
        protected override Color BeamEdge => new(210, 60, 20);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 1120;
            Projectile.tileCollide = false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(sweepDir);
            writer.Write(sweepArc);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            sweepDir = reader.ReadSingle();
            sweepArc = reader.ReadSingle();
        }

        public static new void AllVanish() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HanbaBigLaser>()) {
                    continue;
                }
                proj.ai[1] = 1;
                proj.netUpdate = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (BeamGrow <= 0.6f)
                return false;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * Leng;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamHalfWidth * 1.5f, ref point);
        }

        public override bool? CanDamage() => BeamGrow > 0.6f ? null : false;

        public override void AI() {
            Weith = 6f * BeamGrow;
            Projectile.timeLeft = 1120;
            Leng = 4200;

            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (npc.Alives() && npc.ModNPC is Hanba boss) {
                Projectile.Center = boss.NPC.Center;
            }
            else {
                Projectile.ai[1] = 1f;
            }

            // 扫射角度完全由 Time 确定性推导 (开幕即定, 无追踪 — 公平阀门)
            float baseRot = Projectile.velocity.ToRotation();
            if (Time <= TelegraphTime) {
                Projectile.rotation = baseRot;
            }
            else {
                float sweepT = Time - TelegraphTime - GrowTime;
                if (sweepT > 0) {
                    Projectile.rotation = baseRot + sweepDir * MathF.Min(sweepT * 0.016f, sweepArc);
                    // 扫完即收
                    if (sweepT * 0.016f >= sweepArc && Projectile.ai[1] == 0) {
                        Projectile.ai[1] = 1f;
                    }
                }
                else {
                    Projectile.rotation = baseRot;
                }
            }

            // 张开推进 (预警期不生长)
            if (Projectile.ai[1] == 0) {
                if (Time > TelegraphTime && Projectile.localAI[2] < GrowTime) {
                    Projectile.localAI[2]++;
                    if (Projectile.localAI[2] == 2) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);
                        ACMUtils.AddScreenShake(9f);
                    }
                }
            }
            else {
                Projectile.localAI[2] -= 1.5f;
                if (Projectile.localAI[2] <= 0) {
                    Projectile.Kill();
                    return;
                }
            }

            // 灼柱沿线火尘 (客户端, 量克制)
            if (!Main.dedServ && BeamGrow > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    Vector2 pos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(Leng);
                    pos += VaultUtils.GetNormalVector(Projectile.rotation.ToRotationVector2()) * Main.rand.NextFloat(-BeamHalfWidth, BeamHalfWidth) * 0.7f;
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Torch, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3), 150, Color.OrangeRed, 1.6f);
                    dust.noGravity = true;
                }
            }

            // 持续低频轰鸣 (取 max 不累加)
            ACMUtils.AddScreenShake(BeamGrow * 3f);

            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 dir = Projectile.rotation.ToRotationVector2();

            // 预警期: 细红瞄准线 + 日核蓄力
            if (Time <= TelegraphTime + GrowTime && BeamGrow < 0.99f) {
                float telT = MathHelper.Clamp(Time / (float)TelegraphTime, 0f, 1f);
                Color warnCore = TelegraphColors.Lethal * (0.35f + 0.45f * telT);
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * Leng, 5f + telT * 5f,
                    warnCore, TelegraphColors.Lethal * 0.6f, 0.4f + telT * 0.5f);
                // 日核: 蓄力凝聚的小型焦日
                HanbaVFX.DrawSunDiscAt(Projectile.Center, 0.035f + telT * 0.05f, 0.35f + telT * 0.6f, telT);
            }

            float grow = BeamGrow;
            if (grow > 0.02f) {
                Vector2 endPos = Projectile.Center + dir * Leng;
                ACMShaders.DrawBeam(Projectile.Center, endPos, BeamHalfWidth, BeamCore, BeamEdge, grow);
                if (grow > 0.05f)
                    ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.17f, grow * 0.9f, new Color(255, 210, 120));
            }
            return false;
        }
    }

    // ============================================================
    //  冲击波 — 演出用可视化环 (无判定)
    // ============================================================
    internal class Shockwave : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0)
                ACMUtils.AddScreenShake(3f);
            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            float t = Projectile.ai[0] / 30f;
            Color drawColor = Color.Orange * (1f - t);
            drawColor.A = 0;
            float scaleRate = Projectile.ai[1] <= 0 ? 1f : Projectile.ai[1];
            // 主环 + 外圈残环 (扩散更有层次)
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null,
                drawColor, 0, value.Size() / 2, Projectile.ai[0] / 10f * scaleRate, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null,
                drawColor * 0.4f, 0, value.Size() / 2, Projectile.ai[0] / 8f * scaleRate, SpriteEffects.None, 0);
            return false;
        }
    }

    // ============================================================
    //  蝗群 — 蝗虫过境墙段 / 尸气蝗团 (判定与蝗云视觉逐段一致)
    // ============================================================
    internal class LocustSet : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=模式 (0=墙段, 1=尸气蝗团); ai[1]=预警帧数 (墙段); ai[2]=计时。</summary>
        private ref float Mode => ref Projectile.ai[0];
        private ref float TelegraphTime => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private const float WallRange = 3400f; // 墙段扫掠总程 (预警带长度与之一致)

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 110;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 620;
            Projectile.tileCollide = false;
        }

        private bool InTelegraph => Mode == 0f && Timer < TelegraphTime;

        public override bool? CanDamage() {
            if (InTelegraph)
                return false;
            if (Mode == 1f && Timer < 20)
                return false; // 蝗团显形期无判定
            return null;
        }

        public override void AI() {
            Timer++;

            if (Mode == 0f) {
                WallSegmentAI();
            }
            else {
                SwarmBoltAI();
            }
        }

        private void WallSegmentAI() {
            if (InTelegraph) {
                // 预警期: 定身 (回退本帧位移), 蝗鸣渐强
                Projectile.position -= Projectile.velocity;
                if ((int)Timer == 1 || (int)Timer == (int)(TelegraphTime * 0.6f)) {
                    SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.2f, Volume = 0.5f }, Projectile.Center);
                }
                return;
            }

            if ((int)Timer == (int)TelegraphTime) {
                SoundEngine.PlaySound(SoundID.Item84 with { Pitch = 0.1f }, Projectile.Center);
            }

            // 扫掠期: 匀速直线; 蝗云覆盖判定体
            float traveled = (Timer - TelegraphTime) * Projectile.velocity.Length();
            if (traveled > WallRange) {
                Projectile.Kill();
                return;
            }

            if (Main.dedServ)
                return;

            // 蝗云覆盖判定体 (密度克制: ~1.5 只/帧, 防 PRT 逼近全局上限)
            int cloud = Main.rand.NextBool() ? 2 : 1;
            for (int i = 0; i < cloud; i++) {
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-Projectile.width * 0.6f, Projectile.width * 0.6f),
                    Main.rand.NextFloat(-Projectile.height * 0.6f, Projectile.height * 0.6f));
                PRTLoader.NewParticle<LocustPRT>(pos, Projectile.velocity * Main.rand.NextFloat(0.9f, 1.3f));
            }
        }

        private void SwarmBoltAI() {
            // 尸气蝗团: 缓速漂移 + 极缓追踪 (恒可甩开), 280f 散团自灭
            if (Timer >= 280) {
                Projectile.Kill();
                return;
            }

            Player player = Projectile.Center.FindClosestPlayer(2600, true);
            if (player is not null && Timer > 20 && Timer < 200) {
                Vector2 targetVel = Projectile.SafeDirectionTo(player.Center) * 8f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVel, 0.012f);
            }
            if (Timer >= 200)
                Projectile.velocity *= 0.97f;

            if (Main.dedServ)
                return;

            if (Timer % 3 == 0) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.55f, Projectile.height * 0.55f);
                PRTLoader.NewParticle<LocustPRT>(pos, Projectile.velocity * 0.8f + Main.rand.NextVector2Circular(2f, 2f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (InTelegraph) {
                // 红色预警带: 与本段真实扫掠路径完全一致 (§6.1 视觉=判定)
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float alpha = MathHelper.Clamp(Timer / TelegraphTime, 0f, 1f);
                alpha *= 0.28f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f);

                Color bandColor = Color.Lerp(TelegraphColors.Lethal, new Color(255, 170, 40), 0.3f);
                bandColor.A = 120;

                float rot = Projectile.velocity.ToRotation();
                Rectangle band = new(0, 0, (int)WallRange, Projectile.height);
                Main.spriteBatch.Draw(pixel, Projectile.Center - Main.screenPosition, band, bandColor * alpha,
                    rot, new Vector2(0, band.Height / 2f), 1f, SpriteEffects.None, 0f);
            }
            else if (Mode == 1f) {
                float fade = MathHelper.Clamp(Timer / 20f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
                HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, 1.2f, HanbaVFX.GhostMoss * (0.5f * fade));
            }
            return false;
        }
    }

    // ============================================================
    //  蝗虫粒子 (纯视觉)
    // ============================================================
    internal class LocustPRT : BasePRT
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hanbas/Locust";
        private float waveOffset;

        public override void SetProperty() {
            Lifetime = 130;
            ShouldKillWhenOffScreen = false;
            waveOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            Scale = Main.rand.NextFloat(0.35f, 0.9f);
            Opacity = 0f;
        }

        public override void AI() {
            Rotation = Velocity.ToRotation();

            // 振荡轨迹 + 淡入淡出
            float waveStrength = 2.5f;
            Vector2 normal = Velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Position += normal * (float)Math.Sin(Time / 6f + waveOffset) * waveStrength;

            float lifeT = Time / (float)Lifetime;
            Opacity = MathHelper.Clamp(Time / 10f, 0f, 1f) * MathHelper.Clamp((1f - lifeT) * 4f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            SpriteEffects spriteEffects = Velocity.X > 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null, Color.White * Opacity,
                Rotation + MathHelper.Pi + MathHelper.PiOver4 * Math.Sign(Velocity.X), TexValue.Size() / 2, Scale, spriteEffects, 0);

            return false;
        }
    }

    // ============================================================
    //  赤地焦痕 — 冲刺沿途布下, 阴燃 30f 后延燃成火刺 (可预读离开)
    // ============================================================
    internal class HanbaScorchTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=阴燃引信帧数; ai[1]=计时。</summary>
        private ref float Fuse => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private const int BurstTime = 22;  // 火刺判定窗口
        private const int FadeTime = 20;

        private float BurstT => Timer - Fuse;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
        }

        public override bool? CanDamage() => BurstT >= 0 && BurstT <= BurstTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (BurstT < 0 || BurstT > BurstTime)
                return false;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, 55f, targetHitbox);
        }

        public override void AI() {
            if (Timer == 0) {
                Projectile.timeLeft = (int)Fuse + BurstTime + FadeTime;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            if (Main.dedServ)
                return;

            if (BurstT < 0) {
                // 阴燃: 少量灰烬余烬 (密度随引信推进)
                if (Timer % 5 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        DustID.Torch, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)), 160, default, 1.0f);
                    d.noGravity = true;
                }
            }
            else if ((int)BurstT == 0) {
                // 延燃点火
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = 0.2f }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        Main.rand.NextVector2Circular(2.5f, 5f) - new Vector2(0, 3f), 100, default, Main.rand.NextFloat(1.6f, 2.6f));
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, HanbaVFX.EmberOrange.ToVector3() * 1.2f);
            }
            else if (BurstT <= BurstTime && Timer % 2 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 40f),
                    DustID.Torch, new Vector2(0, -Main.rand.NextFloat(2f, 4f)), 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (BurstT < 0) {
                // 阴燃预警: 暗红余烬点, 随引信渐亮渐红
                float t = Timer / MathF.Max(Fuse, 1f);
                Color warn = Color.Lerp(HanbaVFX.EmberOrange * 0.4f, TelegraphColors.Lethal * 0.85f, t);
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.whoAmI * 1.7f);
                HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, (0.28f + 0.25f * t) * pulse, warn);
            }
            else {
                // 火刺喷发: SlashBurst 向上拉伸 (poly 急速弹出) + 柔光
                float bt = MathHelper.Clamp(BurstT / BurstTime, 0f, 1f);
                float pop = 1f - MathF.Pow(1f - MathF.Min(bt * 3f, 1f), 6f); // 急速弹出
                float fade = 1f - MathHelper.Clamp((BurstT - BurstTime) / FadeTime + bt * 0.4f, 0f, 1f);

                Texture2D burst = ACMAsset.SlashBurst;
                if (burst != null) {
                    Color c = HanbaVFX.EmberOrange * (0.85f * fade);
                    c.A = 0;
                    Vector2 scale = new(0.28f, 0.30f * pop);
                    Main.spriteBatch.Draw(burst, Projectile.Center + new Vector2(0, 30) - Main.screenPosition,
                        null, c, 0f, new Vector2(burst.Width / 2f, burst.Height), scale, SpriteEffects.None, 0f);
                }
                HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, 0.7f * pop * fade, HanbaVFX.SunGold * (0.7f * fade));
            }
            return false;
        }
    }

    // ============================================================
    //  干裂之环 — 干渴汲取: 延迟起爆的环形火刺 (站原地必中, 移动必过)
    // ============================================================
    internal class HanbaCrackRing : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=环半径; ai[1]=引信帧数; ai[2]=计时。</summary>
        private ref float Radius => ref Projectile.ai[0];
        private ref float Fuse => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private const int BurstTime = 24;   // 起爆判定窗口
        private const float BandHalf = 55f; // 环带判定半宽

        private float BurstT => Timer - Fuse;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
        }

        public override bool? CanDamage() => BurstT >= 0 && BurstT <= BurstTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (BurstT < 0 || BurstT > BurstTime)
                return false;
            // 环带判定: 目标中心到环圆周的距离 (含目标半径补偿)
            float dist = Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center);
            float pad = MathF.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return MathF.Abs(dist - Radius) < BandHalf + pad;
        }

        public override void AI() {
            if (Timer == 0) {
                Projectile.timeLeft = (int)Fuse + BurstTime + 26;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            // 焦土环贴花 (每帧发布制): 预警期红调渐亮, 起爆后转焰橙
            float ringWidth = BandHalf / MathF.Max(Radius, 60f) * 1.35f;
            if (BurstT < 0) {
                float t = Timer / MathF.Max(Fuse, 1f);
                Color ember = Color.Lerp(TelegraphColors.Lethal, HanbaVFX.EmberOrange, 0.25f);
                HanbaScorchScreenSystem.AddScorchMark(Projectile.Center, Radius * 1.15f, t, 0.35f + 0.5f * t,
                    ring: true, ringWidth: ringWidth, ember: ember);
            }
            else {
                float fade = 1f - MathHelper.Clamp((BurstT - BurstTime) / 26f, 0f, 1f);
                HanbaScorchScreenSystem.AddScorchMark(Projectile.Center, Radius * 1.15f, 1f, fade,
                    ring: true, ringWidth: ringWidth, ember: HanbaVFX.EmberOrange);
            }

            if ((int)BurstT == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.15f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(4f);
                if (!Main.dedServ) {
                    // 环周火刺尘爆
                    int spikes = (int)(Radius / 46f);
                    for (int i = 0; i < spikes; i++) {
                        float ang = MathHelper.TwoPi * i / spikes;
                        Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Radius;
                        for (int j = 0; j < 5; j++) {
                            Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                                new Vector2(0, -Main.rand.NextFloat(3f, 7f)).RotatedByRandom(0.3f), 100, default, Main.rand.NextFloat(1.6f, 2.8f));
                            d.noGravity = true;
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || BurstT < 0)
                return false;

            // 起爆期: 环周 SlashBurst 火刺圈
            float bt = MathHelper.Clamp(BurstT / BurstTime, 0f, 1f);
            float pop = 1f - MathF.Pow(1f - MathF.Min(bt * 2.6f, 1f), 6f);
            float fade = 1f - MathHelper.Clamp((BurstT - BurstTime * 0.6f) / (BurstTime * 0.7f), 0f, 1f);
            if (fade <= 0.01f)
                return false;

            Texture2D burstTex = ACMAsset.SlashBurst;
            if (burstTex == null)
                return false;

            int count = (int)(Radius / 62f);
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Radius;
                Color c = HanbaVFX.EmberOrange * (0.75f * fade);
                c.A = 0;
                Main.spriteBatch.Draw(burstTex, pos - Main.screenPosition, null, c,
                    ang + MathHelper.PiOver2, new Vector2(burstTex.Width / 2f, burstTex.Height), new Vector2(0.16f, 0.22f * pop), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    // ============================================================
    //  焚天坠日 — set-piece 巨日: 高空凝聚 → 坠落 → 冲击帧 + 双向火波
    // ============================================================
    internal class HanbaSunOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=落点地面 Y; ai[1]=阶段 (0 凝聚 / 1 坠落 / 2 冲击余辉); ai[2]=计时。</summary>
        private ref float LandY => ref Projectile.ai[0];
        private ref float Phase => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private const int ChargeTime = 90;
        private bool didImpactVFX; // 本地冲击演出只跑一次

        private float ChargeT => MathHelper.Clamp(Timer / ChargeTime, 0f, 1f);
        private float GrowScale => Phase >= 1 ? 1f : ChargeT * ChargeT * ChargeT; // 立方生长: 无声开场, 惊人收尾

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
        }

        public override bool? CanDamage() => Phase == 1 || (Phase == 2 && Timer < 18) ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase == 0)
                return false;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, 120f * MathF.Max(GrowScale, 0.6f), targetHitbox);
        }

        public override void AI() {
            Timer++;

            // 落点预警环 (焦土贴花走每帧发布制, 必须在更新阶段登记才会被 PostDrawTiles 绘制)
            if (Phase == 0) {
                float chargeT = ChargeT;
                HanbaScorchScreenSystem.AddScorchMark(new Vector2(Projectile.Center.X, LandY), 340f,
                    chargeT, 0.3f + 0.5f * chargeT, ring: true, ringWidth: 0.2f, ember: TelegraphColors.Lethal);
            }
            else if (Phase == 1) {
                HanbaScorchScreenSystem.AddScorchMark(new Vector2(Projectile.Center.X, LandY), 340f,
                    1f, 0.85f, ring: true, ringWidth: 0.2f, ember: TelegraphColors.Lethal);
            }
            else if (Phase == 2) {
                // 冲击后焦土场自落点扩张 (常驻场由 Boss 端接管)
                float t = MathHelper.Clamp(Timer / 40f, 0f, 1f);
                HanbaScorchScreenSystem.AddScorchMark(Projectile.Center, 900f, t, 0.7f);
            }

            switch ((int)Phase) {
                case 0: // 高空凝聚
                    Projectile.velocity = Vector2.Zero;

                    if (!Main.dedServ) {
                        // 汇聚流线: 密度 ∝ √t, 72% 处硬切 — 尖啸前的静默
                        float charge = ChargeT;
                        if (charge < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(charge) * 0.9f) {
                            Vector2 spawn = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(180f, 520f);
                            Dust d = Dust.NewDustPerfect(spawn, DustID.GoldFlame,
                                (Projectile.Center - spawn) * 0.075f, 100, default, Main.rand.NextFloat(1.4f, 2.4f));
                            d.noGravity = true;
                        }
                        ACMUtils.AddScreenShake(charge * charge * charge * 3f);
                    }

                    if ((int)Timer == (int)(ChargeTime * 0.72f)) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
                    }

                    if (Timer >= ChargeTime) {
                        Phase = 1;
                        Timer = 0;
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.4f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, Projectile.Center);
                        Projectile.netUpdate = true;
                    }
                    break;

                case 1: // 坠落: 加速直下, X 锁定 (落点开幕即定)
                    Projectile.velocity = new Vector2(0, MathF.Min(5f + Timer * 1.35f, 46f));

                    if (!Main.dedServ) {
                        for (int i = 0; i < 3; i++) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(90f, 90f),
                                DustID.Torch, -Projectile.velocity * 0.1f, 100, default, Main.rand.NextFloat(1.8f, 3f));
                            d.noGravity = true;
                        }
                    }

                    if (Projectile.Center.Y >= LandY - 60f) {
                        Phase = 2;
                        Timer = 0;
                        Projectile.velocity = Vector2.Zero;
                        Projectile.Center = new Vector2(Projectile.Center.X, LandY - 60f);
                        Projectile.netUpdate = true;

                        // 服务器: 双向燎原火波
                        if (!VaultUtils.isClient) {
                            for (int s = -1; s <= 1; s += 2) {
                                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                                    Projectile.Center + new Vector2(s * 90, 0), new Vector2(s * 9.5f, 0),
                                    ModContent.ProjectileType<HanbaFireWave>(), Projectile.damage, 2f, Main.myPlayer);
                            }
                        }
                    }
                    break;

                case 2: // 冲击余辉
                    Projectile.velocity = Vector2.Zero;
                    if (!didImpactVFX) {
                        didImpactVFX = true;
                        RunImpactVFX();
                    }
                    // 巨日余辉沉入地面, 焦土场印记由 Boss 端接管常驻
                    if (Timer > 110) {
                        Projectile.Kill();
                    }
                    break;
            }

            Lighting.AddLight(Projectile.Center, HanbaVFX.SunGold.ToVector3() * 2f * GrowScale);
        }

        // 冲击帧: 本战唯一 (白闪 + 震屏 16 + 热浪脉冲)
        private void RunImpactVFX() {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.3f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);
            ACMUtils.AddScreenShake(16f);
            HanbaScorchScreenSystem.FlashWhite(1f);
            HanbaScorchScreenSystem.PulseHeat(0.85f);
            Hanba.NotifySunImpact(Projectile.Center);

            if (Main.dedServ)
                return;
            for (int i = 0; i < 60; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Torch : DustID.GoldFlame,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 16f), 100, default, Main.rand.NextFloat(1.6f, 3.4f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float radiusFrac;
            float flare;
            float intensity = 1f;

            if (Phase == 0) {
                float charge = ChargeT;
                // 预坍缩: 最后 10% 收缩到 ~82% — 变小, 然后变响
                float collapse = charge > 0.9f ? 1f - (charge - 0.9f) * 1.8f : 1f;
                radiusFrac = (0.035f + 0.185f * GrowScale) * collapse;
                flare = charge * 0.8f;

                // 落点预警: 竖直坠落线 (Execution 级预告)
                Vector2 landPos = new(Projectile.Center.X, LandY);
                Color warn = TelegraphColors.Lethal * (0.3f + 0.4f * charge);
                ACMShaders.DrawBeam(Projectile.Center, landPos, 6f + charge * 6f, warn, TelegraphColors.Lethal * 0.55f, 0.5f + charge * 0.4f);
            }
            else if (Phase == 1) {
                radiusFrac = 0.22f;
                flare = 1f;
                Vector2 landPos = new(Projectile.Center.X, LandY);
                ACMShaders.DrawBeam(Projectile.Center, landPos, 14f, TelegraphColors.Lethal * 0.8f, TelegraphColors.Lethal * 0.5f, 0.9f);
            }
            else {
                float t = MathHelper.Clamp(Timer / 110f, 0f, 1f);
                radiusFrac = 0.22f * (1f - t * 0.55f);
                flare = 1f - t;
                intensity = 1f - t * t;
            }

            HanbaVFX.DrawSunDiscAt(Projectile.Center, radiusFrac, intensity, flare);
            return false;
        }
    }

    // ============================================================
    //  燎原火波 — 坠日冲击的地表火浪 (恒速, 高 ~110px, 可跳/可飞越)
    // ============================================================
    internal class HanbaFireWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 110;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 170;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.ai[0]++;

            // 地表吸附: 自上方向下探地, 波顶贴地面
            int tileX = (int)(Projectile.Center.X / 16f);
            int startY = (int)((Projectile.Center.Y - 200f) / 16f);
            for (int tileY = startY; tileY < startY + 44; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    float groundY = tileY * 16f;
                    Projectile.position.Y = MathHelper.Lerp(Projectile.position.Y, groundY - Projectile.height, 0.3f);
                    break;
                }
            }

            Lighting.AddLight(Projectile.Center, HanbaVFX.EmberOrange.ToVector3() * 0.9f);

            if (Main.dedServ)
                return;

            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Bottom + new Vector2(Main.rand.NextFloat(-Projectile.width, Projectile.width) * 0.5f, 0),
                    DustID.Torch, new Vector2(Projectile.velocity.X * 0.3f, -Main.rand.NextFloat(2f, 6f)), 100, default,
                    Main.rand.NextFloat(1.7f, 2.9f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D burst = ACMAsset.SlashBurst;
            if (burst == null)
                return false;

            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f) * MathHelper.Clamp(Projectile.ai[0] / 10f, 0f, 1f);
            float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.whoAmI * 2f);
            Color c = HanbaVFX.EmberOrange * (0.8f * lifeFade * flick);
            c.A = 0;
            Main.spriteBatch.Draw(burst, Projectile.Bottom - Main.screenPosition, null, c, 0f,
                new Vector2(burst.Width / 2f, burst.Height), new Vector2(0.22f, 0.26f), SpriteEffects.None, 0f);
            HanbaVFX.DrawGlow(Main.spriteBatch, Projectile.Center, 0.8f * lifeFade, HanbaVFX.SunGold * (0.5f * lifeFade));
            return false;
        }
    }

    // ============================================================
    //  旱魃蜃景 — 热浪幻影分身 (纯视觉零判定; 无眼光 = 可读真伪)
    // ============================================================
    internal class HanbaMirage : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>ai[0]=起跳延迟帧; ai[1]=冲刺速度; ai[2]=计时。</summary>
        private ref float LaunchDelay => ref Projectile.ai[0];
        private ref float DashSpeed => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private const int DashFrames = 10;
        private const int EvaporateFrames = 26;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = false; // 蜃景零判定 (公平阀门)
            Projectile.damage = 0;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Timer++;

            float launchT = Timer - LaunchDelay;

            if (launchT < 0) {
                // 显形悬浮 + 末 12f 反向抽身 (与本体同拍)
                Projectile.velocity *= 0.9f;
                Player player = Projectile.Center.FindClosestPlayer(3600, true);
                if (player is not null && launchT > -12) {
                    float pull = MathF.Pow((12 + launchT) / 12f, 8f);
                    Projectile.velocity = Projectile.SafeDirectionTo(player.Center) * -pull * 16f;
                }
            }
            else if (launchT == 0) {
                Player player = Projectile.Center.FindClosestPlayer(3600, true);
                Vector2 dir = player is not null
                    ? Projectile.SafeDirectionTo(player.Center + player.velocity * 10f)
                    : Vector2.UnitX * Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);
                Projectile.velocity = dir * DashSpeed;
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            }
            else if (launchT > DashFrames) {
                Projectile.velocity *= 0.62f;
                if (launchT > DashFrames + EvaporateFrames) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Main.dedServ)
                return;
            // 热浪蒸腾尘
            if (Timer % 3 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40f, 60f),
                    DustID.Torch, new Vector2(0, -Main.rand.NextFloat(0.5f, 2f)), 180, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Texture2D bossTex = TextureAssets.Npc[ModContent.NPCType<Hanba>()].Value;
            Rectangle frame = VaultUtils.GetRectangle(bossTex, 0, 4);

            float launchT = Timer - LaunchDelay;
            float alpha = MathHelper.Clamp(Timer / 22f, 0f, 1f) * 0.5f;
            if (launchT > DashFrames)
                alpha *= 1f - MathHelper.Clamp((launchT - DashFrames) / EvaporateFrames, 0f, 1f);

            // 三重波动采样: 海市蜃楼的横向撕裂感
            for (int i = 0; i < 3; i++) {
                float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 2.1f + Projectile.whoAmI) * (4f + i * 3f);
                Color c = Color.Lerp(HanbaVFX.EmberOrange, HanbaVFX.SunGold, i / 2f) * (alpha * (0.55f - i * 0.14f));
                c.A = 0;
                Main.spriteBatch.Draw(bossTex, Projectile.Center + new Vector2(wobble, 0) - Main.screenPosition,
                    frame, c, Projectile.velocity.X * 0.008f, frame.Size() / 2f, 1f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}

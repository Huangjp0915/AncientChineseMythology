using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 符文能量球 — 神威·断罪刃的机制核心弹体（金蓝符文日轮球）。
    ///
    /// ai[0] = 模式:
    ///   0 = 飞弹        — 直线飞行 (ai[1]==1 时首 60 帧轻微追踪后锁直线)
    ///   1 = 符印        — 原地延时引爆 (ai[1]=倒计时帧), 武装期无接触伤害(公平阀门),
    ///                     引爆放出 6 向小飞弹
    ///   2 = 囚阵节点    — 按生成速度缓慢漂移 (静止=速度0), ai[1]=倒计时,
    ///                     ai[2]=total*100+index (链环编号), 引爆沿生成方向放出 2 颗向心飞弹;
    ///                     相邻节点间绘制符能链锁, 末 40 帧白闪预警
    ///   3 = 追踪弹      — 全程缓慢追踪 (转率有限, 90 帧后失去制导)
    /// </summary>
    public class RunicEnergyOrbs : ModProjectile
    {
        private float spinPhase;

        public const float ModeMissile = 0f;
        public const float ModeSeal = 1f;
        public const float ModeCageNode = 2f;
        public const float ModeHoming = 3f;

        private float Mode => Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        // 武装中的符印不造成接触伤害 — 危险全部来自可读的延时引爆 (防原地放印贴脸秒)
        public override bool? CanDamage() => Mode == ModeSeal ? false : null;

        // 符印/囚阵节点由 ai[1] 倒计时驱动生死, 不吃默认位移; 节点漂移速度由生成时 velocity 决定
        public override bool ShouldUpdatePosition() => Mode != ModeSeal;

        public override void AI() {
            switch (Mode) {
                case ModeSeal: SealAI(); return;
                case ModeCageNode: CageNodeAI(); return;
                case ModeHoming: HomingAI(); break;
                default: MissileAI(); break;
            }

            // —— 飞行体通用视觉 ——
            Projectile.rotation += 0.1f;
            spinPhase += 0.15f;

            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * 12f;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, dustType, 0, 0, 120, default, 1.2f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.2f);
        }

        private void MissileAI() {
            // ai[1]==1: 齐射预测弹 — 首 60 帧朝目标微调航向, 之后锁直线 (公平: 不无限追)
            if (Projectile.ai[1] == 1f && Projectile.timeLeft > 240) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.03f);
                }
            }
        }

        private void HomingAI() {
            // 有限追踪: 转率随寿命衰减, 90 帧后完全失去制导
            float life = 300f - Projectile.timeLeft;
            if (life < 90f) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float turnRate = MathHelper.Lerp(0.045f, 0f, life / 90f);
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Vector2 current = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                    Vector2 dir = Vector2.Lerp(current, desired, turnRate).SafeNormalize(Vector2.UnitY);
                    Projectile.velocity = dir * Projectile.velocity.Length();
                }
            }
        }

        private void SealAI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.06f;
            spinPhase += 0.1f;

            Projectile.ai[1]--;

            // 引爆将近 = 粒子收束半径缩小 + 提亮 (可读预警)
            float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 150f, 0f, 1f);
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = MathHelper.Lerp(38f, 16f, urgency);
                Vector2 offset = angle.ToRotationVector2() * dist;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, DustID.GoldFlame, 0, 0, 80, default, 1f + urgency);
                d.noGravity = true;
                d.velocity = (Projectile.Center - d.position).SafeNormalize(Vector2.Zero) * (2f + urgency * 2f);
            }
            if (urgency > 0.6f && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 60, default, 1.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.45f, 0.15f) * (0.5f + urgency * 0.8f));

            if (Projectile.ai[1] <= 0) {
                Projectile.ai[2] = 1f; // 正式引爆标记 — 区别于换阶段清弹 (清弹不放载荷)
                Projectile.Kill();
            }
        }

        private void CageNodeAI() {
            // 首帧记录生成方向 (向心方向, 引爆载荷沿此方向)
            if (Projectile.localAI[0] == 0f && Projectile.localAI[1] == 0f) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.localAI[0] = dir.X;
                Projectile.localAI[1] = dir.Y;
            }

            Projectile.rotation += 0.04f;
            spinPhase += 0.08f;
            Projectile.ai[1]--;

            float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 40f, 0f, 1f); // 末 40 帧白闪
            if (Main.rand.NextBool(4)) {
                int dustType = urgency > 0.01f ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(14, 14), 0, 0, dustType, 0, 0, 100, default, 1.1f + urgency * 0.8f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.42f, 0.2f) * (0.6f + urgency * 0.7f));

            if (Projectile.ai[1] <= 0) {
                Projectile.ai[2] = 1f; // 正式引爆标记
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            bool isSeal = Mode == ModeSeal;
            bool isNode = Mode == ModeCageNode;
            int count = isSeal || isNode ? 20 : 10;
            float scale = isSeal || isNode ? 2.5f : 1.5f;
            float speed = isSeal || isNode ? 8f : 5f;

            for (int i = 0; i < count; i++) {
                int dustType = i % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, scale);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(speed, speed);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            bool detonated = Projectile.ai[2] == 1f; // 只有倒计时正式引爆才放载荷 (换阶段清弹不放)

            // 符印引爆: 6 向小飞弹
            if (isSeal && detonated) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6 * i + 0.26f;
                    Vector2 vel = angle.ToRotationVector2() * 6.5f;
                    Projectile p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        Type, Projectile.damage / 2, 0f, Projectile.owner);
                    p.timeLeft = 110;
                }
            }
            // 囚阵节点引爆: 沿生成方向 (向心) 放出 2 颗飞弹
            else if (isNode && detonated) {
                Vector2 dir = new Vector2(Projectile.localAI[0], Projectile.localAI[1]).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = dir.RotatedBy(i * 0.09f) * 7f;
                    Projectile p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        Type, Projectile.damage / 2, 0f, Projectile.owner);
                    p.timeLeft = 160;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (Mode == ModeSeal)
                DrawSealTelegraph(sb, glow, drawPos);
            else if (Mode == ModeCageNode)
                DrawCageLinks(sb, drawPos);

            float pulse = 1f + MathF.Sin(spinPhase * 4f) * 0.15f;

            // 飞行体残影 (静止模式无意义, 跳过)
            if (Mode != ModeSeal && Mode != ModeCageNode) {
                for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float t = (float)i / Projectile.oldPos.Length;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(255, 200, 80), new Color(80, 130, 220), t) * (0.4f * (1f - t));
                    trailColor.A = 0;
                    sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                        Projectile.scale * (1f - t * 0.3f), SpriteEffects.None, 0f);
                }
                // 迎头光晕 — 速度门控 (越快越亮)
                if (glow != null) {
                    float speedGlow = MathHelper.Clamp(Projectile.velocity.Length() / 16f, 0.2f, 1f);
                    Color head = new Color(255, 230, 150) * (0.35f * speedGlow);
                    head.A = 0;
                    sb.Draw(glow, drawPos, null, head, 0f, glow.Size() / 2f, 0.9f * pulse, SpriteEffects.None, 0f);
                }
            }

            Color mainColor = Color.Lerp(new Color(255, 215, 100), new Color(100, 150, 255), MathF.Sin(spinPhase) * 0.5f + 0.5f);

            // 囚阵节点: 末 40 帧向白金过曝 (齐爆预警)
            if (Mode == ModeCageNode) {
                float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 40f, 0f, 1f);
                if (urgency > 0f) {
                    float flicker = 0.5f + MathF.Sin(spinPhase * 14f) * 0.5f;
                    mainColor = Color.Lerp(mainColor, Color.White, urgency * (0.55f + flicker * 0.45f));
                    pulse += urgency * 0.35f;
                }
            }

            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);
            return false;
        }

        // 符印: 引爆将近由暗转亮的可读地纹光环 (暖金=危险预警)
        private void DrawSealTelegraph(SpriteBatch sb, Texture2D glow, Vector2 gPos) {
            if (glow == null)
                return;
            float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 150f, 0f, 1f);
            Vector2 gOrigin = glow.Size() / 2f;
            float ringPulse = 0.5f + MathF.Sin(spinPhase * 3f) * 0.5f;

            // 外环: 随引爆将近收缩+提亮
            float outerScale = (0.55f - urgency * 0.18f + ringPulse * 0.06f) * 0.6f;
            Color outer = Color.Lerp(new Color(180, 130, 40), new Color(255, 210, 90), urgency) * (0.35f + urgency * 0.5f);
            outer.A = 0;
            sb.Draw(glow, gPos, null, outer, 0f, gOrigin, outerScale, SpriteEffects.None, 0f);

            // 内核: 引爆临界白金闪
            if (urgency > 0.6f) {
                Color core = Color.Lerp(new Color(255, 220, 120), Color.White, (urgency - 0.6f) / 0.4f) * (urgency * 0.6f);
                core.A = 0;
                sb.Draw(glow, gPos, null, core, 0f, gOrigin, outerScale * 0.5f * ringPulse, SpriteEffects.None, 0f);
            }
        }

        // 囚阵链锁: 与最近两个同模式节点之间的符能连线 (由暗转亮 = 引爆预告)
        private void DrawCageLinks(SpriteBatch sb, Vector2 drawPos) {
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave == null)
                return;

            float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 90f, 0f, 1f);
            Color linkColor = Color.Lerp(new Color(130, 100, 40), new Color(255, 215, 110), urgency) * (0.16f + urgency * 0.30f);
            linkColor.A = 0;

            int drawn = 0;
            for (int i = 0; i < Main.maxProjectiles && drawn < 2; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.type != Type || other.whoAmI <= Projectile.whoAmI)
                    continue;
                if (other.ai[0] != ModeCageNode)
                    continue;
                float dist = Vector2.Distance(other.Center, Projectile.Center);
                if (dist > 430f || dist < 8f)
                    continue;

                Vector2 toOther = other.Center - Projectile.Center;
                float rot = toOther.ToRotation();
                // GlaciateWave 512 宽, 横向拉伸为细链
                sb.Draw(wave, drawPos + toOther * 0.5f, null, linkColor, rot,
                    wave.Size() * 0.5f, new Vector2(dist / wave.Width, 0.028f + urgency * 0.02f), SpriteEffects.None, 0f);
                drawn++;
            }
        }
    }
}
